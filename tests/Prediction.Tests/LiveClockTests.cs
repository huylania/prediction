using Prediction.Core.Engine.LiveCorners;
using Prediction.Core.Entities;

namespace Prediction.Tests;

public class LiveClockTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 30, DateTimeKind.Utc);

    [Fact]
    public void Estimated_minute_adds_time_since_last_update_and_caps_per_half()
    {
        Assert.Equal(24, LiveClock.EstimatedMinute("1H", 22, Now.AddMinutes(-2), Now));
        Assert.Equal(22, LiveClock.EstimatedMinute("1H", 22, Now.AddSeconds(-50), Now));
        Assert.Equal(50, LiveClock.EstimatedMinute("1H", 44, Now.AddMinutes(-20), Now));
        Assert.Equal(45, LiveClock.EstimatedMinute("HT", null, Now.AddMinutes(-5), Now));
        Assert.Equal(76, LiveClock.EstimatedMinute("2H", 73, Now.AddMinutes(-3), Now));
        Assert.Equal(98, LiveClock.EstimatedMinute("2H", 90, Now.AddMinutes(-30), Now));
        Assert.Equal(22, LiveClock.EstimatedMinute("1H", 22, Now.AddMinutes(1), Now)); // đồng hồ lệch: không trừ
        Assert.Null(LiveClock.EstimatedMinute("NS", null, Now, Now));
        Assert.Null(LiveClock.EstimatedMinute("FT", 90, Now, Now));
    }

    [Fact]
    public void Hot_window_covers_minutes_before_and_through_golden_windows()
    {
        var zone = new HotZone();
        Assert.Null(LiveClock.HotWindow("1H", 21, zone));
        Assert.Equal(CornerWindow.FirstHalf, LiveClock.HotWindow("1H", 22, zone));
        Assert.Equal(CornerWindow.FirstHalf, LiveClock.HotWindow("1H", 31, zone));
        Assert.Null(LiveClock.HotWindow("1H", 32, zone));
        Assert.Null(LiveClock.HotWindow("HT", 45, zone));
        Assert.Equal(CornerWindow.SecondHalf, LiveClock.HotWindow("2H", 72, zone));
        Assert.Equal(CornerWindow.SecondHalf, LiveClock.HotWindow("2H", 81, zone));
        Assert.Null(LiveClock.HotWindow("2H", 82, zone));
        Assert.Null(LiveClock.HotWindow("2H", null, zone));
        Assert.Equal(CornerWindow.FirstHalf, LiveClock.HotWindow("1H", 20, new HotZone(H1From: 20)));
    }

    [Fact]
    public void Fast_cycle_runs_only_when_enabled_hot_with_quota_and_on_cadence()
    {
        Assert.True(LiveClock.ShouldRunFast(30, 2, 5000, 1500, null, Now));
        Assert.False(LiveClock.ShouldRunFast(0, 2, 5000, 1500, null, Now)); // tắt
        Assert.False(LiveClock.ShouldRunFast(30, 0, 5000, 1500, null, Now)); // không có trận nóng
        Assert.False(LiveClock.ShouldRunFast(30, 2, 1200, 1500, null, Now)); // quota thấp
        Assert.True(LiveClock.ShouldRunFast(30, 2, null, 1500, null, Now)); // chưa biết quota
        Assert.False(LiveClock.ShouldRunFast(30, 2, 5000, 1500, Now.AddSeconds(-15), Now));
        Assert.True(LiveClock.ShouldRunFast(30, 2, 5000, 1500, Now.AddSeconds(-29), Now));
    }
}
