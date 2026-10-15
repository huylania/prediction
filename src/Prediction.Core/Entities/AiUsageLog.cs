namespace Prediction.Core.Entities;

/// <summary>Tính năng của website có gọi API Claude (tốn tiền theo token).</summary>
public enum AiFeature
{
    /// <summary>"Viết báo cáo AI" ở tab Phân tích chuyên sâu của trận (bấm tay).</summary>
    Report = 1,

    /// <summary>"AI điền thông tin bên lề" — tác vụ nền tự chạy cho trận nhóm A/B.</summary>
    SideInfoAuto = 2,

    /// <summary>"AI điền thông tin bên lề" — bấm tay ở tab Lịch sử & bên lề.</summary>
    SideInfoManual = 3,

    /// <summary>Góc Live: Claude đọc ảnh AiScore (bấm tay).</summary>
    CornerVision = 4,

    /// <summary>Góc Live: "Claude nhận định" (bấm tay).</summary>
    CornerCommentary = 5,
}

/// <summary>Một lần gọi API Claude: token, lượt tra web, chi phí ước tính — kể cả lần lỗi (vẫn có thể bị tính tiền).</summary>
public class AiUsageLog
{
    public long Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public AiFeature Feature { get; set; }

    public string Model { get; set; } = "";

    /// <summary>Trận liên quan (không ràng buộc khóa ngoại để log còn nguyên khi xóa trận).</summary>
    public int? FixtureId { get; set; }

    /// <summary>Mô tả ngắn, vd tên trận.</summary>
    public string? Subject { get; set; }

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheReadTokens { get; set; }
    public int CacheWriteTokens { get; set; }
    public int WebSearches { get; set; }

    /// <summary>Chi phí ước tính (USD) theo bảng giá; số chính xác xem ở console.anthropic.com.</summary>
    public decimal CostUsd { get; set; }

    public int DurationMs { get; set; }

    public bool Ok { get; set; }

    public string? Error { get; set; }
}

/// <summary>Cài đặt đổi được ngay trên website (ghi đè appsettings).</summary>
public class AppSetting
{
    public string Key { get; set; } = "";

    public string? Value { get; set; }
}

public static class AiFeatureText
{
    public static string Label(AiFeature feature) => feature switch
    {
        AiFeature.Report => "Báo cáo AI (phân tích chuyên sâu)",
        AiFeature.SideInfoAuto => "Thông tin bên lề — TỰ ĐỘNG",
        AiFeature.SideInfoManual => "Thông tin bên lề — bấm tay",
        AiFeature.CornerVision => "Góc Live — đọc ảnh AiScore",
        AiFeature.CornerCommentary => "Góc Live — Claude nhận định",
        _ => feature.ToString(),
    };
}
