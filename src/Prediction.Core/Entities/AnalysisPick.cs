using Prediction.Core.Engine.Analysis;

namespace Prediction.Core.Entities;

/// <summary>
/// Khuyến nghị của bộ phân tích chuyên sâu cho một loại kèo của một trận. Được cập nhật tới giờ bóng lăn
/// (bản cuối trước giờ bóng lăn là bản chốt), rồi chấm khi có kết quả để theo dõi thành tích nhóm A/B.
/// </summary>
public class AnalysisPick
{
    public long Id { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public Market Market { get; set; }

    public PickSide Side { get; set; }

    /// <summary>Mức kèo nhìn từ cửa được chọn.</summary>
    public decimal Line { get; set; }

    public decimal Odds { get; set; }

    public decimal Score { get; set; }

    public AnalysisGroup Group { get; set; }

    /// <summary>Xác suất thắng (kể cả thắng nửa) theo mô hình.</summary>
    public decimal WinProbability { get; set; }

    public decimal ExpectedValue { get; set; }

    public string Label { get; set; } = "";

    /// <summary>Số hiệu cờ đỏ, vd "2,5".</summary>
    public string? Flags { get; set; }

    /// <summary>Số trận làm mẫu (đội ít dữ liệu hơn). Dưới 5 trận: "ít dữ liệu", tối đa nhóm B.</summary>
    public int? Sample { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public BetOutcome Outcome { get; set; }

    public decimal? Profit { get; set; }

    public DateTime? SettledAtUtc { get; set; }
}
