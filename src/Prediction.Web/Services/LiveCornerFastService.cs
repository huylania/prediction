using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prediction.Core.Engine.LiveCorners;
using Prediction.Web.ApiFootball;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>Trạng thái Góc Live nhanh để hiện trên trang.</summary>
public sealed record LiveCornerFastStatus(bool Enabled, int Seconds, HotZone Zone, int HotFixtures, DateTime? LastRunUtc, string? PausedReason);

/// <summary>
/// Góc Live nhanh: tiến trình nền riêng (không xếp hàng sau các tác vụ nặng). Cứ 15 giây xem trong CSDL có trận nào ở vùng nóng
/// quanh cửa sổ vàng (mặc định phút 22–31 và 72–81, theo phút ước lượng) không; có thì cứ <see cref="SyncOptions.LiveCornerFastSeconds"/> giây
/// chạy một chu kỳ: số liệu các trận đó + kèo live + chốt khuyến nghị. Quota còn thấp thì tạm dừng, nhịp 2 phút vẫn chạy như cũ.
/// </summary>
public sealed class LiveCornerFastService(
    SyncCoordinator coordinator,
    ApiFootballClient api,
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<SyncOptions> options,
    TimeProvider clock,
    ILogger<LiveCornerFastService> logger) : BackgroundService
{
    public static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(15);

    private DateTime? _lastRunUtc;

    public LiveCornerFastStatus Status { get; private set; } = new(false, 0, new HotZone(), 0, null, null);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (!o.Enabled || o.LiveCornerFastSeconds <= 0)
        {
            Status = new LiveCornerFastStatus(false, o.LiveCornerFastSeconds, o.HotZone, 0, null, !o.Enabled ? "đồng bộ nền đang tắt" : "LiveCornerFastSeconds = 0");
            return;
        }

        using var timer = new PeriodicTimer(CheckEvery);
        do
        {
            try
            {
                await TickAsync(o, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Góc Live nhanh lỗi");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TickAsync(SyncOptions o, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (!api.IsConfigured)
        {
            Status = new LiveCornerFastStatus(true, o.LiveCornerFastSeconds, o.HotZone, 0, _lastRunUtc, "chưa có API key");
            return;
        }

        var hot = await HotFixturesAsync(o.HotZone, now, ct);
        var remaining = ApiFootballClient.RemainingToday;
        var paused = remaining is { } r && r < o.LiveCornerFastMinRemaining ? $"quota còn {r} < {o.LiveCornerFastMinRemaining}" : null;
        Status = new LiveCornerFastStatus(true, o.LiveCornerFastSeconds, o.HotZone, hot.Count, _lastRunUtc, paused);
        if (!LiveClock.ShouldRunFast(o.LiveCornerFastSeconds, hot.Count, remaining, o.LiveCornerFastMinRemaining, _lastRunUtc, now))
        {
            return;
        }

        _lastRunUtc = now;
        await coordinator.RunFastCornersAsync(hot, ct);
        Status = Status with { LastRunUtc = _lastRunUtc };
    }

    /// <summary>Trận đang đá (giải theo dõi, có thống kê) đang ở vùng nóng theo phút ước lượng.</summary>
    private async Task<List<int>> HotFixturesAsync(HotZone zone, DateTime now, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var live = await db.Fixtures.AsNoTracking()
            .Where(f => f.League!.IsTracked && !f.League.IsDemo && f.League.HasStatsCoverage
                && f.KickoffUtc <= now && f.KickoffUtc >= now.AddHours(-3) && (f.Status == "1H" || f.Status == "2H"))
            .Select(f => new { f.Id, f.Status, f.Elapsed, f.UpdatedAtUtc })
            .ToListAsync(ct);
        return live
            .Where(f => LiveClock.HotWindow(f.Status, LiveClock.EstimatedMinute(f.Status, f.Elapsed, f.UpdatedAtUtc, now), zone) is not null)
            .Select(f => f.Id)
            .ToList();
    }
}
