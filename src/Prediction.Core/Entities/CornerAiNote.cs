namespace Prediction.Core.Entities;

/// <summary>Loại lần gọi Claude ở Góc Live.</summary>
public enum CornerAiKind
{
    /// <summary>Claude đọc ảnh AiScore.</summary>
    ReadImages = 1,

    /// <summary>"Claude nhận định".</summary>
    Commentary = 2,
}

/// <summary>Kết quả một lần gọi Claude ở Góc Live (đọc ảnh / nhận định), lưu để xem lại và so với kết quả trận.</summary>
public class CornerAiNote
{
    public long Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public CornerAiKind Kind { get; set; }

    /// <summary>Trận website đang theo dõi (null nếu nhập tay / đọc ảnh trận khác). Không ràng buộc khóa ngoại.</summary>
    public int? FixtureId { get; set; }

    /// <summary>Tên trận, vd "Arsenal – Chelsea (Premier League)".</summary>
    public string Match { get; set; } = "";

    public CornerWindow Window { get; set; }

    public int Minute { get; set; }

    public int HomeCorners { get; set; }

    public int AwayCorners { get; set; }

    public int HomeGoals { get; set; }

    public int AwayGoals { get; set; }

    /// <summary>Mốc kèo góc live lúc gọi.</summary>
    public decimal? Line { get; set; }

    public decimal? OverOdds { get; set; }

    public decimal? UnderOdds { get; set; }

    /// <summary>Kỳ vọng tổng góc của công thức lúc gọi.</summary>
    public double? ExpectedTotal { get; set; }

    /// <summary>Kết luận của công thức lúc gọi.</summary>
    public string? Headline { get; set; }

    /// <summary>Số liệu Claude đọc từ ảnh, hoặc dữ liệu đã gửi khi nhận định (JSON).</summary>
    public string? DataJson { get; set; }

    /// <summary>Bài nhận định (markdown) hoặc ghi chú Claude đọc từ ảnh.</summary>
    public string? Markdown { get; set; }

    public string Model { get; set; } = "";

    public decimal CostUsd { get; set; }

    /// <summary>false = dở dang (lỗi hoặc bấm Dừng giữa chừng); phần đã nhận vẫn được lưu vì đã tính tiền.</summary>
    public bool Complete { get; set; } = true;

    public int TotalCorners => HomeCorners + AwayCorners;
}
