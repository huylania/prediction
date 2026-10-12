namespace Prediction.Core.Entities;

/// <summary>
/// Gợi ý sinh ra khi một quy tắc khớp với trận sắp đá. Được cập nhật theo kèo tới giờ bóng lăn,
/// sau đó "đóng băng" theo kèo đóng và được chấm khi có kết quả.
/// </summary>
public class Suggestion
{
    public long Id { get; set; }

    public int RuleId { get; set; }
    public Rule? Rule { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public PickSide Side { get; set; }

    /// <summary>Mức kèo nhìn từ cửa được chọn (chấp: âm = cửa đó chấp; tài xỉu: tổng bàn).</summary>
    public decimal FirstLine { get; set; }
    public decimal FirstOdds { get; set; }
    public DateTime FirstMatchedAtUtc { get; set; }

    public decimal Line { get; set; }
    public decimal Odds { get; set; }

    public int? SubjectRank { get; set; }
    public int? OpponentRank { get; set; }

    /// <summary>Trước giờ bóng lăn: quy tắc hiện còn khớp hay không.</summary>
    public bool CurrentlyMatches { get; set; } = true;

    /// <summary>Đã chốt theo kèo đóng (sau giờ bóng lăn).</summary>
    public bool IsFrozen { get; set; }

    public BetOutcome Outcome { get; set; }

    /// <summary>Lãi/lỗ trên 1 đơn vị cược.</summary>
    public decimal? Profit { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? SettledAtUtc { get; set; }
}
