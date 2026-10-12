namespace Prediction.Core.Entities;

/// <summary>
/// Một "kinh nghiệm bet" dưới dạng bộ lọc. Mọi điều kiện đều tùy chọn và được AND với nhau.
/// "Đội A" là đội được xét (subject), "Đội B" là đối thủ. Mỗi trận được xét từ cả hai góc nhìn.
/// </summary>
public class Rule
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public int? LeagueId { get; set; }
    public League? League { get; set; }

    public Market Market { get; set; } = Market.AsianHandicap;

    public RuleSelection Selection { get; set; } = RuleSelection.Subject;

    // --- BXH & phong độ ---

    public int? SubjectTeamId { get; set; }
    public Team? SubjectTeam { get; set; }

    public VenueFilter SubjectVenue { get; set; }

    /// <summary>A xếp trên B ít nhất N bậc.</summary>
    public int? MinRankGap { get; set; }

    /// <summary>A xếp trên B nhiều nhất N bậc (âm = A xếp dưới).</summary>
    public int? MaxRankGap { get; set; }

    /// <summary>A nằm trong top N.</summary>
    public int? SubjectMaxRank { get; set; }

    /// <summary>B xếp từ hạng N trở xuống.</summary>
    public int? OpponentMinRank { get; set; }

    /// <summary>A hơn B ít nhất N điểm.</summary>
    public int? MinPointsGap { get; set; }

    /// <summary>A có ít nhất N điểm trong 5 trận gần nhất.</summary>
    public int? SubjectMinFormPoints { get; set; }

    /// <summary>B có nhiều nhất N điểm trong 5 trận gần nhất.</summary>
    public int? OpponentMaxFormPoints { get; set; }

    /// <summary>Trung bình tổng bàn/trận trong 5 trận gần nhất của hai đội.</summary>
    public decimal? MinAvgTotalGoals { get; set; }
    public decimal? MaxAvgTotalGoals { get; set; }

    /// <summary>Khi xét điều kiện thứ hạng/điểm: cả hai đội phải đá ít nhất N trận.</summary>
    public int? MinMatchesPlayed { get; set; }

    // --- Kèo hiện tại ---

    /// <summary>Mức chấp nhìn từ phía A (âm = A chấp).</summary>
    public decimal? AhLineMin { get; set; }
    public decimal? AhLineMax { get; set; }

    public decimal? OuLineMin { get; set; }
    public decimal? OuLineMax { get; set; }

    /// <summary>Odds tối thiểu của cửa được chọn.</summary>
    public decimal? MinOdds { get; set; }

    // --- Biến động kèo ---

    public AhMovementFilter AhMovement { get; set; }

    /// <summary>Mức chấp phải dịch ít nhất bao nhiêu (mặc định 0.25).</summary>
    public decimal? MinAhLineMove { get; set; }

    /// <summary>Khi mức kèo đứng yên: tiền kèo phải giảm ít nhất bao nhiêu (mặc định 0.05).</summary>
    public decimal? MinPriceDrop { get; set; }

    public OuMovementFilter OuMovement { get; set; }

    public decimal? MinOuLineMove { get; set; }

    // --- Tư vấn API ---

    public bool RequireApiWinnerSubject { get; set; }

    public int? MinApiWinPercentSubject { get; set; }

    /// <summary>Chỉ với quy tắc tài xỉu: API dự đoán cùng hướng tài/xỉu.</summary>
    public bool RequireApiUnderOverAgree { get; set; }

    public bool UsesTableConditions =>
        MinRankGap.HasValue || MaxRankGap.HasValue || SubjectMaxRank.HasValue ||
        OpponentMinRank.HasValue || MinPointsGap.HasValue;

    /// <summary>Có điều kiện phân biệt được đội A với đội B (không chỉ dựa vào kèo).</summary>
    public bool HasSubjectConditions =>
        SubjectTeamId.HasValue || SubjectVenue != VenueFilter.Any || UsesTableConditions ||
        SubjectMinFormPoints.HasValue || OpponentMaxFormPoints.HasValue;

    public bool UsesMarketData =>
        AhLineMin.HasValue || AhLineMax.HasValue || OuLineMin.HasValue || OuLineMax.HasValue ||
        MinOdds.HasValue || AhMovement != AhMovementFilter.Any || OuMovement != OuMovementFilter.Any;

    public bool UsesApiAdvice =>
        RequireApiWinnerSubject || MinApiWinPercentSubject.HasValue ||
        (RequireApiUnderOverAgree && Market == Market.OverUnder);

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return "Cần đặt tên quy tắc.";
        }

        if (Market == Market.AsianHandicap && Selection is not (RuleSelection.Subject or RuleSelection.Opponent))
        {
            yield return "Kèo chấp phải chọn Đội A hoặc Đội B.";
        }

        if (Market == Market.OverUnder && Selection is not (RuleSelection.Over or RuleSelection.Under))
        {
            yield return "Kèo tài xỉu phải chọn Tài hoặc Xỉu.";
        }

        foreach (var (name, value) in new[] { ("Mức chấp từ", AhLineMin), ("Mức chấp đến", AhLineMax), ("Tài xỉu từ", OuLineMin), ("Tài xỉu đến", OuLineMax) })
        {
            if (value.HasValue && value.Value * 4 != decimal.Truncate(value.Value * 4))
            {
                yield return $"{name} phải là bội số của 0.25.";
            }
        }

        if (AhLineMin > AhLineMax)
        {
            yield return "Mức chấp: giá trị 'từ' phải nhỏ hơn hoặc bằng 'đến'.";
        }

        if (OuLineMin > OuLineMax)
        {
            yield return "Tài xỉu: giá trị 'từ' phải nhỏ hơn hoặc bằng 'đến'.";
        }

        if (MinRankGap > MaxRankGap)
        {
            yield return "Chênh lệch hạng: 'ít nhất' phải nhỏ hơn hoặc bằng 'nhiều nhất'.";
        }
    }
}
