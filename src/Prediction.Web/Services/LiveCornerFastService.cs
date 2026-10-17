using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prediction.Core.Engine.LiveCorners;
using Prediction.Web.ApiFootball;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>Trạng thái Góc Live nhanh để hiện trên trang.</summary>
/// <param name="Global">Góc Live toàn cầu đang bật (gói Mega).</param>
/// <param name="GlobalHot">Trong số trận ở vùng nóng, bao nhiêu trận thuộc giải không theo dõi (do Góc Live toàn cầu thêm).</param>
public sealed record LiveCornerFastStatus(bool Enabled, int Seconds, HotZone Zone, int HotFixtures, DateTime? LastRunUtc, string? PausedReason,
    bool Global = false, int GlobalHot = 0);

/// <summary>
/// Góc Live nhanh: tiến trình nền riêng (không xếp hàng sau các tác vụ nặng). Cứ 5 giây xem trong CSDL có trận nào ở vùng nóng
/// quanh cửa sổ vàng (mặc định phút 22–31 và 72–81, theo phút ước lượng) không; có thì cứ <see cref="SyncOptions.LiveCornerFastSeconds"/> giây
/// chạy một chu kỳ: số liệu các trận đó + kèo live + chốt khuyến nghị. Quota còn thấp thì tạm dừng, nhịp thường (Pro 2 phút, Ultra 1 phút) vẫn chạy như cũ.
/// </summary>
public sealed class LiveCornerFastService(
    SyncCoordinator coordinator,
    ApiFootballClient api,
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<SyncOptions> options,
    TimeProvider clock,
    ILogger<LiveCornerFastService> logger) : BackgroundService
{
    /// <summary>Nhịp kiểm tra; phải nhỏ để chu kỳ thật bám đúng số giây cấu hình (15 giây sẽ làm 20 giây thành 30).</summary>
    public static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(5);

    private DateTime? _lastRunUtc;
    private DateTime? _lastGlobalUtc;

    /// <summary>Góc Live toàn cầu: gọi fixtures?live=all mỗi chừng này (1 request/lần).</summary>
    public static readonly TimeSpan GlobalEvery = TimeSpan.FromSeconds(60);

    public LiveCornerFastStatus Status { get; private set; } = new(false, 0, new HotZone(), 0, null, null);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            Status = new LiveCornerFastStatus(false, 0, options.Value.HotZone, 0, null, "đồng bộ nền đang tắt");
            return;
        }

        using var timer = new PeriodicTimer(CheckEvery);
        do
        {
            try
            {
                // Hồ sơ Pro/Ultra đọc lại mỗi lượt: giây/lần và ngưỡng quota theo gói đang dùng.
                await TickAsync(options.Value.Effective(), stoppingToken);
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
        if (o.LiveCornerFastSeconds <= 0)
        {
            Status = new LiveCornerFastStatus(false, o.LiveCornerFastSeconds, o.HotZone, 0, _lastRunUtc, "LiveCornerFastSeconds = 0");
            return;
        }

        if (!api.IsConfigured)
        {
            Status = new LiveCornerFastStatus(true, o.LiveCornerFastSeconds, o.HotZone, 0, _lastRunUtc, "chưa có API key");
            return;
        }

        var remaining = ApiFootballClient.RemainingToday;
        var paused = remaining is { } r && r < o.LiveCornerFastMinRemaining ? $"quota còn {r} < {o.LiveCornerFastMinRemaining}" : null;
        if (o.LiveCornerGlobal && paused is null && (_lastGlobalUtc is not { } lg || now - lg >= GlobalEvery - TimeSpan.FromSeconds(2)))
        {
            _lastGlobalUtc = now;
            await coordinator.RunGlobalLiveAsync(ct);
        }

        var hot = await HotFixturesAsync(o.HotZone, o.LiveCornerGlobal, now, ct);
        Status = new LiveCornerFastStatus(true, o.LiveCornerFastSeconds, o.HotZone, hot.Count, _lastRunUtc, paused,
            o.LiveCornerGlobal, hot.Count(h => !h.Tracked));
        if (!LiveClock.ShouldRunFast(o.LiveCornerFastSeconds, hot.Count, remaining, o.LiveCornerFastMinRemaining, _lastRunUtc, now))
        {
            return;
        }

        _lastRunUtc = now;
        await coordinator.RunFastCornersAsync(hot.Select(h => h.Id).ToList(), ct);
        Status = Status with { LastRunUtc = _lastRunUtc };
    }

    /// <summary>
    /// Trận đang đá (giải theo dõi, hoặc giải Góc Live toàn cầu đã thêm khi <paramref name="global"/>; có thống kê) đang ở vùng nóng
    /// theo phút ước lượng.
    /// </summary>
    private async Task<List<(int Id, bool Tracked)>> HotFixturesAsync(HotZone zone, bool global, DateTime now, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var live = await db.Fixtures.AsNoTracking()
            .Where(f => (f.League!.IsTracked || global && f.League.LiveScan) && !f.League.IsDemo && f.League.HasStatsCoverage
                && f.KickoffUtc <= now && f.KickoffUtc >= now.AddHours(-3) && (f.Status == "1H" || f.Status == "2H"))
            .Select(f => new { f.Id, f.Status, f.Elapsed, f.UpdatedAtUtc, f.League!.IsTracked })
            .ToListAsync(ct);
        return live
            .Where(f => LiveClock.HotWindow(f.Status, LiveClock.EstimatedMinute(f.Status, f.Elapsed, f.UpdatedAtUtc, now), zone) is not null)
            .Select(f => (f.Id, f.IsTracked))
            .ToList();
    }
}
