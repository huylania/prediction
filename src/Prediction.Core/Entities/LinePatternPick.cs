using Prediction.Core.Engine.Analysis;

namespace Prediction.Core.Entities;

/// <summary>
/// Trận khớp mẫu kèo Tài 3.0 / chấp 0.75 / chấp 1.0 cửa trên, kèm điểm của bộ chỉ số "giờ + điểm".
/// Cập nhật tới giờ bóng lăn (bản cuối là bản chốt), rồi chấm khi có kết quả để biết điểm cao có thắng thật không.
/// </summary>
public class LinePatternPick
{
    public long Id { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public LinePattern Pattern { get; set; }

    public PickSide Side { get; set; }

    /// <summary>Mức kèo nhìn từ cửa được chọn (Tài: 3; cửa trên: −0.75 / −1).</summary>
    public decimal Line { get; set; }

    public decimal Odds { get; set; }

    public decimal Score { get; set; }

    public AnalysisGroup Group { get; set; }

    /// <summary>Xác suất thắng (kể cả thắng nửa), đã trộn với giá nhà cái.</summary>
    public decimal WinProbability { get; set; }

    public decimal PushProbability { get; set; }

    public decimal ExpectedValue { get; set; }

    public decimal? MinOdds { get; set; }

    public string Label { get; set; } = "";

    /// <summary>Gợi ý giờ vào kèo lúc chốt.</summary>
    public string? EntryText { get; set; }

    /// <summary>Tóm tắt các chỉ số, vd "Xác suất +1.2; EV +0.4; Sân +0.5".</summary>
    public string? Indicators { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public BetOutcome Outcome { get; set; }

    public decimal? Profit { get; set; }

    public DateTime? SettledAtUtc { get; set; }
}
