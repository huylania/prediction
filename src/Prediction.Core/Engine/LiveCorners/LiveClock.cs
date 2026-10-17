using Prediction.Core.Entities;

namespace Prediction.Core.Engine.LiveCorners;

/// <summary>Vùng phút cần quét dày quanh hai cửa sổ vàng (mặc định H1 22–31, H2 72–81).</summary>
public sealed record HotZone(int H1From = 22, int H1To = 31, int H2From = 72, int H2To = 81);

/// <summary>
/// Đồng hồ trận đang đá giữa hai lần gọi API: phút API trả về (Elapsed) cộng thời gian đã trôi từ lần cập nhật cuối,
/// để biết trận nào sắp/đang ở cửa sổ vàng mà không cần gọi API.
/// </summary>
public static class LiveClock
{
    /// <summary>Phút trận ước lượng lúc <paramref name="nowUtc"/>; null nếu trận không đang đá.</summary>
    public static int? EstimatedMinute(string status, int? elapsed, DateTime updatedAtUtc, DateTime nowUtc)
    {
        var drift = Math.Max(0, (int)Math.Floor((nowUtc - updatedAtUtc).TotalMinutes));
        return status switch
        {
            "1H" => Math.Min((elapsed ?? 0) + drift, 50),
            "HT" => 45,
            "2H" => Math.Min(Math.Max(elapsed ?? 46, 46) + drift, 98),
            _ => null,
        };
    }

    /// <summary>Trận đang ở vùng nóng của cửa sổ nào (null nếu không).</summary>
    public static CornerWindow? HotWindow(string status, int? minute, HotZone zone) => (status, minute) switch
    {
        ("1H", { } m) when m >= zone.H1From && m <= zone.H1To => CornerWindow.FirstHalf,
        ("2H", { } m) when m >= zone.H2From && m <= zone.H2To => CornerWindow.SecondHalf,
        _ => null,
    };

    /// <summary>
    /// Có chạy chu kỳ quét nhanh lúc này không: bật (giây &gt; 0), có trận nóng, còn đủ quota, và đã qua đủ số giây từ lần trước.
    /// </summary>
    public static bool ShouldRunFast(int fastSeconds, int hotFixtures, int? remainingQuota, int minRemaining, DateTime? lastRunUtc, DateTime nowUtc)
    {
        if (fastSeconds <= 0 || hotFixtures == 0)
        {
            return false;
        }

        if (remainingQuota is { } r && r < minRemaining)
        {
            return false;
        }

        // Cho lệch 2 giây vì vòng kiểm tra chạy theo nhịp riêng.
        return lastRunUtc is not { } last || (nowUtc - last).TotalSeconds >= fastSeconds - 2;
    }
}
