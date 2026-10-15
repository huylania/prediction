using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prediction.Core.Entities;
using Prediction.Web.ApiFootball;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

public static class SyncJobs
{
    public const string Leagues = "leagues";
    public const string Fixtures = "fixtures";
    public const string Odds = "odds";
    public const string ClosingOdds = "closing-odds";
    public const string Advice = "advice";
    public const string AdviceFinal = "advice-final";
    public const string Suggestions = "suggestions";
    public const string Live = "live";
    public const string Backfill = "backfill";
    public const string Stats = "stats";
    public const string Coaches = "coaches";
    public const string Injuries = "injuries";
    public const string Analysis = "analysis";

    /// <summary>Tác vụ phụ: tạm dừng khi quota trong ngày sắp hết.</summary>
    public static bool IsLowPriority(string job) => job is Stats or Coaches or Injuries or Advice;

    public static string Label(string job) => job switch
    {
        Leagues => "Giải đấu",
        Fixtures => "Lịch & kết quả",
        Odds => "Kèo theo giải",
        ClosingOdds => "Kèo sát giờ",
        Advice => "Tư vấn API",
        AdviceFinal => "Tư vấn API sát giờ",
        Suggestions => "Gợi ý",
        Live => "Trực tiếp",
        Backfill => "Tải mùa cũ",
        Stats => "Thống kê trận (góc, thẻ, sút)",
        Coaches => "HLV",
        Injuries => "Chấn thương",
        Analysis => "Phân tích chuyên sâu",
        _ => job,
    };
}

/// <summary>
/// Chạy các tác vụ đồng bộ tuần tự (không chồng lên nhau), ghi nhật ký và cập nhật gợi ý sau mỗi tác vụ.
/// Dùng chung cho tiến trình nền và các nút "Đồng bộ ngay".
/// </summary>
public sealed class SyncCoordinator(
    IServiceScopeFactory scopes,
    IDbContextFactory<AppDbContext> dbFactory,
    SuggestionService suggestions,
    AnalysisService analysis,
    TimeProvider clock,
    ILogger<SyncCoordinator> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string? CurrentJob { get; private set; }

    public event Action? Changed;

    /// <summary>
    /// Chạy một tác vụ. <paramref name="work"/> trả về thông điệp, hoặc null nếu không có gì để làm (không ghi nhật ký).
    /// </summary>
    public async Task<SyncLog?> RunAsync(string job, Func<DataSyncService, CancellationToken, Task<string?>> work, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        var started = clock.GetUtcNow().UtcDateTime;
        CurrentJob = job;
        Changed?.Invoke();
        SyncLog? log = null;
        try
        {
            using var scope = scopes.CreateScope();
            var sync = scope.ServiceProvider.GetRequiredService<DataSyncService>();
            string? message;
            var ok = true;
            try
            {
                message = await work(sync, ct);
                if (job is not (SyncJobs.Suggestions or SyncJobs.Analysis))
                {
                    var suggestionMessage = await suggestions.RefreshAsync(ct);
                    message = message is null || suggestionMessage is null ? message : $"{message} {suggestionMessage}";
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                ok = false;
                message = ex is ApiFootballException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
                logger.LogError(ex, "Tác vụ đồng bộ {Job} lỗi", job);
            }

            if (message is not null)
            {
                log = new SyncLog
                {
                    Job = job,
                    StartedAtUtc = started,
                    FinishedAtUtc = clock.GetUtcNow().UtcDateTime,
                    Ok = ok,
                    Requests = sync.RequestCount,
                    Message = message.Length > 2000 ? message[..2000] : message,
                };
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                db.SyncLogs.Add(log);
                await db.SaveChangesAsync(ct);
                var expired = started.AddDays(-14);
                await db.SyncLogs.Where(l => l.StartedAtUtc < expired).ExecuteDeleteAsync(ct);
            }

            return log;
        }
        finally
        {
            CurrentJob = null;
            _lock.Release();
            Changed?.Invoke();
        }
    }

    public Task<SyncLog?> RunNamedAsync(string job, CancellationToken ct) => job switch
    {
        SyncJobs.Leagues => RunAsync(job, async (s, c) => await s.RefreshLeaguesAsync(c), ct),
        SyncJobs.Fixtures => RunAsync(job, async (s, c) => await s.SyncCurrentFixturesAsync(c), ct),
        SyncJobs.Odds => RunAsync(job, async (s, c) => await s.SyncLeagueOddsAsync(c), ct),
        SyncJobs.ClosingOdds => RunAsync(job, (s, c) => s.SyncClosingOddsAsync(c), ct),
        SyncJobs.Advice => RunAsync(job, (s, c) => s.SyncAdviceAsync(false, c), ct),
        SyncJobs.AdviceFinal => RunAsync(job, (s, c) => s.SyncAdviceAsync(true, c), ct),
        SyncJobs.Suggestions => RunAsync(job, async (_, c) => await suggestions.RefreshAsync(c), ct),
        SyncJobs.Live => RunAsync(job, (s, c) => s.SyncLiveAsync(c), ct),
        SyncJobs.Stats => RunAsync(job, (s, c) => s.SyncStatsAsync(null, c), ct),
        SyncJobs.Coaches => RunAsync(job, (s, c) => s.SyncCoachesAsync(c), ct),
        SyncJobs.Injuries => RunAsync(job, (s, c) => s.SyncInjuriesAsync(c), ct),
        SyncJobs.Analysis => RunAsync(job, (_, c) => analysis.RefreshPicksAsync(c), ct),
        _ => throw new ArgumentOutOfRangeException(nameof(job)),
    };
}

/// <summary>Tiến trình nền: chạy các tác vụ đồng bộ theo chu kỳ trong cấu hình "Sync".</summary>
public sealed class SyncBackgroundService(
    SyncCoordinator coordinator,
    ApiFootballClient api,
    IOptions<SyncOptions> options,
    TimeProvider clock,
    ILogger<SyncBackgroundService> logger) : BackgroundService
{
    private readonly Dictionary<string, DateTime> _lastRun = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (!o.Enabled)
        {
            logger.LogInformation("Đồng bộ nền đang tắt (Sync:Enabled = false).");
            return;
        }

        if (!api.IsConfigured)
        {
            logger.LogWarning("Chưa cấu hình ApiFootball:ApiKey — chỉ cập nhật gợi ý, không lấy dữ liệu mới.");
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            foreach (var (job, minutes) in Schedule(o))
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                var now = clock.GetUtcNow().UtcDateTime;
                if (_lastRun.TryGetValue(job, out var last) && now - last < TimeSpan.FromMinutes(minutes))
                {
                    continue;
                }

                if (SyncJobs.IsLowPriority(job) && DataSyncService.QuotaLow(o))
                {
                    continue; // thử lại ở phút sau, khi quota được làm mới
                }

                _lastRun[job] = now;
                try
                {
                    await coordinator.RunNamedAsync(job, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Lỗi khi chạy tác vụ {Job}", job);
                }
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private IEnumerable<(string Job, int Minutes)> Schedule(SyncOptions o)
    {
        if (api.IsConfigured)
        {
            yield return (SyncJobs.Leagues, o.LeaguesIntervalMinutes);
            yield return (SyncJobs.Fixtures, o.FixturesIntervalMinutes);
            yield return (SyncJobs.Odds, o.OddsIntervalMinutes);
            yield return (SyncJobs.ClosingOdds, o.ClosingOddsIntervalMinutes);
            yield return (SyncJobs.Advice, o.AdviceIntervalMinutes);
            yield return (SyncJobs.AdviceFinal, 15);
            yield return (SyncJobs.Live, o.LiveIntervalMinutes);
            yield return (SyncJobs.Injuries, o.InjuriesIntervalMinutes);
            yield return (SyncJobs.Coaches, o.CoachesIntervalMinutes);
            yield return (SyncJobs.Stats, o.StatsIntervalMinutes);
        }

        yield return (SyncJobs.Suggestions, o.SuggestionsIntervalMinutes);
        yield return (SyncJobs.Analysis, o.AnalysisIntervalMinutes);
    }
}
