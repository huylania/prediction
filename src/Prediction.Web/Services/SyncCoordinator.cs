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
    public const string Lineups = "lineups";
    public const string PreKickoff = "pre-kickoff";
    public const string HeadToHead = "h2h";
    public const string Venues = "venues";
    public const string Weather = "weather";
    public const string SideInfo = "side-info";
    public const string LiveCorners = "live-corners";
    public const string LiveOdds = "live-odds";

    /// <summary>Góc Live nhanh: số liệu + kèo góc live + chốt khuyến nghị cho các trận ở vùng nóng (chạy riêng, không chờ tác vụ nặng).</summary>
    public const string LiveFast = "live-fast";

    /// <summary>Góc Live toàn cầu: fixtures?live=all mỗi phút, thêm trận đang đá của giải không theo dõi nhưng có thống kê (gói Mega).</summary>
    public const string LiveGlobal = "live-global";

    /// <summary>Chạy trong tiến trình Góc Live nhanh: chỉ giữ khóa live, không xếp hàng sau tác vụ nặng.</summary>
    public static bool IsFastLane(string job) => job is LiveFast or LiveGlobal;

    /// <summary>Tác vụ ghi dữ liệu trận đang đá: dùng chung khóa live với Góc Live nhanh để không ghi chồng lên nhau.</summary>
    public static bool TouchesLive(string job) => job is Live or LiveOdds or LiveCorners;

    /// <summary>Tác vụ phụ: tạm dừng khi quota trong ngày sắp hết.</summary>
    public static bool IsLowPriority(string job) => job is Stats or Coaches or Injuries or Advice or HeadToHead or Venues;

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
        Lineups => "Đội hình",
        PreKickoff => "Kèo 5 phút trước trận",
        HeadToHead => "Lịch sử đối đầu",
        Venues => "Sân vận động",
        Weather => "Thời tiết",
        SideInfo => "AI điền thông tin bên lề",
        LiveCorners => "Góc Live",
        LiveOdds => "Kèo live",
        LiveFast => "Góc Live nhanh",
        LiveGlobal => "Góc Live toàn cầu",
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
    SideInfoService sideInfo,
    LiveCornerService liveCorners,
    TimeProvider clock,
    ILogger<SyncCoordinator> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Khóa riêng cho dữ liệu trận đang đá. Góc Live nhanh chỉ giữ khóa này nên không phải chờ các tác vụ nặng (kèo theo giải, tư vấn…);
    /// các tác vụ live thường (Trực tiếp, Kèo live, Góc Live) giữ cả hai khóa.
    /// </summary>
    private readonly SemaphoreSlim _liveLock = new(1, 1);

    public string? CurrentJob { get; private set; }

    /// <summary>Góc Live nhanh đang chạy (không chặn các nút đồng bộ khác).</summary>
    public string? CurrentLiveJob { get; private set; }

    public event Action? Changed;

    /// <summary>
    /// Chạy một tác vụ. <paramref name="work"/> trả về thông điệp, hoặc null nếu không có gì để làm (không ghi nhật ký).
    /// </summary>
    public async Task<SyncLog?> RunAsync(string job, Func<DataSyncService, CancellationToken, Task<string?>> work, CancellationToken ct)
    {
        var fast = SyncJobs.IsFastLane(job);
        var live = fast || SyncJobs.TouchesLive(job);
        if (!fast)
        {
            await _lock.WaitAsync(ct);
        }

        if (live)
        {
            try
            {
                await _liveLock.WaitAsync(ct);
            }
            catch
            {
                if (!fast)
                {
                    _lock.Release();
                }

                throw;
            }
        }

        var started = clock.GetUtcNow().UtcDateTime;
        if (fast)
        {
            CurrentLiveJob = job;
        }
        else
        {
            CurrentJob = job;
        }

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
                if (job is not (SyncJobs.Suggestions or SyncJobs.Analysis or SyncJobs.SideInfo or SyncJobs.LiveCorners or SyncJobs.LiveOdds or SyncJobs.LiveFast or SyncJobs.LiveGlobal))
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
            if (fast)
            {
                CurrentLiveJob = null;
            }
            else
            {
                CurrentJob = null;
            }

            if (live)
            {
                _liveLock.Release();
            }

            if (!fast)
            {
                _lock.Release();
            }

            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Một chu kỳ Góc Live nhanh: số liệu các trận ở vùng nóng (20 trận/request) → kèo live (1 request) → chốt khuyến nghị (không gọi API).
    /// Chỉ ghi nhật ký khi có khuyến nghị được chốt/chấm.
    /// </summary>
    public Task<SyncLog?> RunFastCornersAsync(IReadOnlyCollection<int> hotFixtureIds, CancellationToken ct) =>
        RunAsync(SyncJobs.LiveFast, async (s, c) =>
        {
            await s.SyncLiveAsync(hotFixtureIds, c);
            await s.SyncLiveOddsAsync(hotFixtureIds, c);
            return await liveCorners.ProcessAsync(c);
        }, ct);

    public Task<SyncLog?> RunGlobalLiveAsync(CancellationToken ct) => RunAsync(SyncJobs.LiveGlobal, (s, c) => s.SyncGlobalLiveAsync(c), ct);

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
        SyncJobs.Lineups => RunAsync(job, (s, c) => s.SyncLineupsAsync(c), ct),
        SyncJobs.PreKickoff => RunAsync(job, (s, c) => s.SyncPreKickoffOddsAsync(c), ct),
        SyncJobs.HeadToHead => RunAsync(job, (s, c) => s.SyncHeadToHeadAsync(c), ct),
        SyncJobs.Venues => RunAsync(job, (s, c) => s.SyncVenuesAsync(c), ct),
        SyncJobs.Weather => RunAsync(job, (s, c) => s.SyncWeatherAsync(c), ct),
        SyncJobs.SideInfo => RunAsync(job, (_, c) => sideInfo.RunAutoAsync(c), ct),
        SyncJobs.LiveCorners => RunAsync(job, (_, c) => liveCorners.ProcessAsync(c), ct),
        SyncJobs.LiveOdds => RunAsync(job, async (s, c) =>
        {
            var (goals, corners) = await s.SyncLiveOddsAsync(c);
            return goals + corners == 0 ? null : $"Kèo live: {goals} thay đổi kèo chấp/tài xỉu, {corners} thay đổi kèo góc.";
        }, ct),
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
    private string? _plan;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
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
            // Đọc lại hồ sơ mỗi phút: gói Ultra (giới hạn ngày lớn) dùng chu kỳ dày hơn.
            var o = options.Value.Effective();
            var plan = options.Value.PlanName(ApiFootballClient.DailyLimit);
            if (plan != _plan)
            {
                logger.LogInformation("Hồ sơ đồng bộ: {Plan} (giới hạn ngày {Limit})", plan, ApiFootballClient.DailyLimit?.ToString() ?? "chưa biết");
                _plan = plan;
            }

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
            yield return (SyncJobs.PreKickoff, o.PreKickoffIntervalMinutes);
            yield return (SyncJobs.Lineups, o.LineupsIntervalMinutes);
            yield return (SyncJobs.Live, o.LiveIntervalMinutes);
            yield return (SyncJobs.LiveOdds, o.LiveOddsIntervalMinutes);
            yield return (SyncJobs.Injuries, o.InjuriesIntervalMinutes);
            yield return (SyncJobs.Coaches, o.CoachesIntervalMinutes);
            yield return (SyncJobs.Stats, o.StatsIntervalMinutes);
            yield return (SyncJobs.HeadToHead, o.H2HIntervalMinutes);
            yield return (SyncJobs.Venues, o.VenuesIntervalMinutes);
        }

        yield return (SyncJobs.Weather, o.WeatherIntervalMinutes);
        yield return (SyncJobs.SideInfo, o.SideInfoIntervalMinutes);
        yield return (SyncJobs.LiveCorners, o.LiveCornersIntervalMinutes);

        yield return (SyncJobs.Suggestions, o.SuggestionsIntervalMinutes);
        yield return (SyncJobs.Analysis, o.AnalysisIntervalMinutes);
    }
}
