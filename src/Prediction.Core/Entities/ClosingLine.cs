namespace Prediction.Core.Entities;

public enum LineSource
{
    Manual = 1,
    Ai = 2,
}

/// <summary>
/// Kèo đóng của một trận cũ mà website không tự ghi được (API không giữ kèo trận đã đá): nhập tay hoặc AI tra cứu web.
/// Khóa là id trận trên API-Football (cả trận không nằm trong bảng Fixtures, vd đối đầu ở cúp).
/// </summary>
public class ClosingLine
{
    public int FixtureId { get; set; }

    /// <summary>Mức chấp của đội nhà trận đó (âm = đội nhà chấp).</summary>
    public decimal? HandicapLine { get; set; }

    public decimal? TotalLine { get; set; }

    public LineSource Source { get; set; }

    /// <summary>Nguồn tham khảo (vd đường dẫn web AI dùng).</summary>
    public string? Note { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
