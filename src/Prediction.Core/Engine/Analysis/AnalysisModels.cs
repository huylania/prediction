using Prediction.Core.Entities;

namespace Prediction.Core.Engine.Analysis;

/// <summary>Một trận đã đá của một đội, nhìn từ phía đội đó.</summary>
public sealed record TeamMatch(
    int FixtureId,
    DateTime KickoffUtc,
    bool IsHome,
    int OpponentId,
    int GoalsFor,
    int GoalsAgainst,
    int? CornersFor = null,
    int? CornersAgainst = null,
    int? CardsFor = null,
    int? CardsAgainst = null,
    decimal? XgFor = null,
    decimal? XgAgainst = null,
    int? ShotsFor = null,
    int? ShotsOnTargetFor = null,
    decimal? ClosingHandicap = null)
{
    public int TotalGoals => GoalsFor + GoalsAgainst;

    public char Result => GoalsFor > GoalsAgainst ? 'W' : GoalsFor == GoalsAgainst ? 'D' : 'L';
}

/// <summary>Trung bình của giải (mùa hiện tại + mùa trước).</summary>
public sealed record LeagueBaseline(int Matches, double? AvgGoals, int StatMatches, double? AvgCorners, double? AvgCards)
{
    public const double DefaultGoals = 2.7;
    public const double DefaultCorners = 10.0;
    public const double DefaultCards = 4.2;

    public static readonly LeagueBaseline Empty = new(0, null, 0, null, null);

    public double Goals => Matches >= 20 && AvgGoals is { } g ? g : DefaultGoals;

    public double Corners => StatMatches >= 20 && AvgCorners is { } c ? c : DefaultCorners;

    public double Cards => StatMatches >= 20 && AvgCards is { } c ? c : DefaultCards;
}

/// <summary>Mọi dữ liệu cần để phân tích trước trận một trận đấu.</summary>
public sealed class AnalysisInput
{
    public required Fixture Fixture { get; init; }

    public string HomeName { get; init; } = "Đội nhà";

    public string AwayName { get; init; } = "Đội khách";

    public string? LeagueName { get; init; }

    public string? LeagueType { get; init; }

    public LeagueDna? LeagueDna { get; init; }

    public TeamDna? HomeDna { get; init; }

    public TeamDna? AwayDna { get; init; }

    /// <summary>BXH trước trận (null nếu không có BXH).</summary>
    public TeamSnapshot? HomeStanding { get; init; }

    public TeamSnapshot? AwayStanding { get; init; }

    /// <summary>Số đội trên BXH (để biết nửa trên / nửa dưới).</summary>
    public int? TableSize { get; init; }

    /// <summary>Các trận đã đá trước trận này của đội nhà, mới nhất trước (nên có ≥ 10 trận nếu đủ dữ liệu).</summary>
    public IReadOnlyList<TeamMatch> HomeHistory { get; init; } = [];

    public IReadOnlyList<TeamMatch> AwayHistory { get; init; } = [];

    public LeagueBaseline League { get; init; } = LeagueBaseline.Empty;

    public MarketMovement? Handicap { get; init; }

    public MarketMovement? Totals { get; init; }

    public MarketMovement? Corners { get; init; }

    public MarketMovement? CornersHandicap { get; init; }

    public MarketMovement? Cards { get; init; }

    public IReadOnlyList<TeamChange> HomeChanges { get; init; } = [];

    public IReadOnlyList<TeamChange> AwayChanges { get; init; } = [];

    public IReadOnlyList<FixtureInjury> Injuries { get; init; } = [];

    public ApiAdvice? Advice { get; init; }

    /// <summary>Các cửa (kèo chấp / tài xỉu bàn) mà quy tắc của bạn đang gợi ý cho trận này.</summary>
    public IReadOnlyList<PickSide> RuleSides { get; init; } = [];
}

public enum AnalysisGroup
{
    /// <summary>Khả dụng trước trận (8.0–10).</summary>
    A = 1,

    /// <summary>Chờ In-play / quan sát (6.0–7.9).</summary>
    B = 2,

    /// <summary>Loại bỏ / chờ ổn định (&lt; 6 hoặc dính cờ đỏ).</summary>
    C = 3,
}

public static class AnalysisGroupText
{
    public static string Label(AnalysisGroup group) => group switch
    {
        AnalysisGroup.A => "Nhóm A – Khả dụng trước trận",
        AnalysisGroup.B => "Nhóm B – Chờ In-play",
        _ => "Nhóm C – Loại / chờ ổn định",
    };

    public static string Short(AnalysisGroup group) => group switch
    {
        AnalysisGroup.A => "A",
        AnalysisGroup.B => "B",
        _ => "C",
    };
}

/// <summary>Tình trạng HLV mới / tân binh trụ cột của một đội tại thời điểm trận đấu.</summary>
public sealed record RegimeStatus(TeamChangeKind Kind, string PersonName, DateTime StartDate, int MatchesUnder)
{
    /// <summary>Trận sắp đá là trận thứ mấy dưới thời HLV/tân binh mới.</summary>
    public int UpcomingMatchNumber => MatchesUnder + 1;

    /// <summary>Đang trong 1–3 trận đầu (vùng nhiễu động).</summary>
    public bool InBuffer => UpcomingMatchNumber <= 3;

    /// <summary>Đã qua giai đoạn ổn định nhưng mẫu dữ liệu mới còn ngắn hơn 10 trận: dùng riêng dữ liệu giai đoạn mới.</summary>
    public bool UsesNewSample => !InBuffer && MatchesUnder < 10;

    public string KindLabel => Kind == TeamChangeKind.NewManager ? "HLV mới" : "Tân binh trụ cột";
}

/// <summary>Thống kê mẫu phong độ của một đội (10 trận gần nhất hoặc giai đoạn HLV/tân binh mới).</summary>
public sealed class TeamForm
{
    public int Matches { get; init; }

    public string SampleLabel { get; init; } = "";

    /// <summary>Kết quả, mới nhất trước (vd "WDLWW").</summary>
    public string Form { get; init; } = "";

    public double GoalsFor { get; init; }

    public double GoalsAgainst { get; init; }

    public double TotalGoals => GoalsFor + GoalsAgainst;

    public double Over25Rate { get; init; }

    public double DrawRate { get; init; }

    public double WinRate { get; init; }

    public int CornerMatches { get; init; }

    public double? CornersFor { get; init; }

    public double? CornersAgainst { get; init; }

    public double? TotalCorners => CornersFor + CornersAgainst;

    public double? Over95CornersRate { get; init; }

    /// <summary>Góc kiếm / góc bị theo đúng sân của trận sắp đá (đội nhà: sân nhà; đội khách: sân khách).</summary>
    public int VenueCornerMatches { get; init; }

    public double? VenueCornersFor { get; init; }

    public double? VenueCornersAgainst { get; init; }

    public int XgMatches { get; init; }

    public double? XgFor { get; init; }

    public double? XgAgainst { get; init; }

    public double? ShotsFor { get; init; }

    public double? CardsFor { get; init; }

    public double? CardsAgainst { get; init; }

    public int AtsMatches { get; init; }

    /// <summary>Tỉ lệ thắng kèo chấp (tính cả thắng nửa) trên các trận có kèo đóng.</summary>
    public double? AtsRate { get; init; }
}

public enum HandicapScenario
{
    None = 0,

    /// <summary>Xếp sát nhau trên BXH nhưng chấp sâu bất thường.</summary>
    DeepLineCloseRanks = 1,

    /// <summary>Đội xếp trên đá sân nhà mà kèo chỉ đồng banh / chấp 1/4 (kèo thơm ảo).</summary>
    ShallowLineForBetterHome = 2,
}

public enum HandicapVerdict
{
    Normal = 0,

    /// <summary>Kèo lạ có nguyên nhân rõ: kèo thực lực.</summary>
    RealStrength = 1,

    /// <summary>Kèo lạ không tìm thấy nguyên nhân: nghi bẫy.</summary>
    SuspectedTrap = 2,
}

/// <summary>Kèo chấp dự phóng H_exp và đối chiếu với kèo nhà cái.</summary>
public sealed class HandicapModel
{
    /// <summary>Phần phong độ: (λ nhà − λ khách) từ 10 trận.</summary>
    public double FormPart { get; init; }

    /// <summary>Phần BXH: chênh hiệu số/trận cả mùa ÷ 2 (null nếu chưa đủ 3 trận).</summary>
    public double? TablePart { get; init; }

    public double HomeAdvantage { get; init; }

    public IReadOnlyList<string> HomeAdvantageNotes { get; init; } = [];

    /// <summary>Độ vượt trội kỳ vọng của đội nhà (bàn).</summary>
    public double Supremacy { get; init; }

    /// <summary>Kèo chấp dự phóng theo đội nhà (âm = nhà chấp).</summary>
    public decimal HExp { get; init; }

    public decimal? HMarket { get; init; }

    public decimal? HOpen { get; init; }

    /// <summary>Δ_H = H_market − H_exp.</summary>
    public decimal? Delta => HMarket - HExp;

    /// <summary>Kèo chấp dịch từ lúc mở (mức của đội nhà).</summary>
    public decimal? LineMove => HMarket - HOpen;

    public bool IsAnomaly { get; init; }

    public HandicapScenario Scenario { get; init; }

    public IReadOnlyList<string> Causes { get; init; } = [];

    public HandicapVerdict Verdict { get; init; }

    public string Summary { get; init; } = "";
}

/// <summary>Một dòng của bảng đối chiếu 3 lớp (bàn thắng, phạt góc, chấp góc, thẻ).</summary>
public sealed class LayerRow
{
    public required string Name { get; init; }

    /// <summary>Trung bình giải (null với chấp góc).</summary>
    public double? LeagueAverage { get; init; }

    public string LeagueDnaText { get; init; } = "";

    public string HomeText { get; init; } = "";

    public string AwayText { get; init; } = "";

    /// <summary>Kỳ vọng kết hợp (tổng) hoặc chênh lệch (chấp góc).</summary>
    public double? Expected { get; init; }

    public decimal? MarketLine { get; init; }

    public string MarketText { get; init; } = "";

    public double? Delta { get; init; }

    /// <summary>Cửa được ủng hộ theo quy tắc (null nếu chưa đạt ngưỡng).</summary>
    public PickSide? Lean { get; init; }

    public string Verdict { get; init; } = "";
}

public sealed record RedFlag(int Number, string Title, string Detail, IReadOnlyList<(Market Market, PickSide? Side)> Applies)
{
    /// <summary>Cờ có áp lên cửa này không (Side null = cả hai cửa của loại kèo).</summary>
    public bool Covers(Market market, PickSide side) => Applies.Any(a => a.Market == market && (a.Side is null || a.Side == side));
}

/// <summary>Một khuyến nghị cụ thể (loại kèo + cửa + mức + odds) kèm điểm và lý do.</summary>
public sealed class AnalysisPickResult
{
    public required Market Market { get; init; }

    public required PickSide Side { get; init; }

    /// <summary>Mức kèo nhìn từ cửa được chọn.</summary>
    public required decimal Line { get; init; }

    public required decimal Odds { get; init; }

    public required PickProbability Probability { get; init; }

    public double ExpectedValue => Probability.ExpectedValue(Odds);

    /// <summary>Đạt ngưỡng khuyến nghị của file kỹ năng (Δ, DNA, phong cách...).</summary>
    public bool MeetsRule { get; init; }

    public double Score { get; set; }

    public AnalysisGroup Group { get; set; }

    public List<string> Reasons { get; } = [];

    public List<int> Flags { get; } = [];

    public string Label { get; init; } = "";
}

/// <summary>Kết quả phân tích trước trận theo cấu trúc mục 9 của file kỹ năng.</summary>
public sealed class MatchAnalysis
{
    public required int FixtureId { get; init; }

    // 1. DNA & nhân sự
    public IReadOnlyList<string> LeagueTags { get; init; } = [];

    public IReadOnlyList<string> HomeTags { get; init; } = [];

    public IReadOnlyList<string> AwayTags { get; init; } = [];

    public string? LeagueNotes { get; init; }

    public string? HomeNotes { get; init; }

    public string? AwayNotes { get; init; }

    public RegimeStatus? HomeRegime { get; init; }

    public RegimeStatus? AwayRegime { get; init; }

    public IReadOnlyList<FixtureInjury> HomeAbsent { get; init; } = [];

    public IReadOnlyList<FixtureInjury> AwayAbsent { get; init; } = [];

    public double? HomeRestDays { get; init; }

    public double? AwayRestDays { get; init; }

    // 2. Kèo chấp dự phóng
    public required HandicapModel Handicap { get; init; }

    // 3. Cờ đỏ
    public IReadOnlyList<RedFlag> Flags { get; init; } = [];

    // 4. Bảng 3 lớp
    public required TeamForm HomeForm { get; init; }

    public required TeamForm AwayForm { get; init; }

    public required LayerRow Goals { get; init; }

    public required LayerRow CornersRow { get; init; }

    public required LayerRow CornerHandicap { get; init; }

    public required LayerRow CardsRow { get; init; }

    // Mô hình xác suất
    public double LambdaHome { get; init; }

    public double LambdaAway { get; init; }

    public double CornersHome { get; init; }

    public double CornersAway { get; init; }

    public double ExpectedCards { get; init; }

    public double HomeWinProbability { get; init; }

    public double DrawProbability { get; init; }

    public double AwayWinProbability { get; init; }

    // 5. Kịch bản In-play
    public IReadOnlyList<string> InPlayHints { get; init; } = [];

    // 6. Khuyến nghị
    public IReadOnlyList<AnalysisPickResult> Picks { get; init; } = [];

    public AnalysisPickResult? Best => Picks.FirstOrDefault();

    public AnalysisGroup Group => Best?.Group ?? AnalysisGroup.C;

    /// <summary>Thiếu dữ liệu phong độ: kết quả chỉ mang tính tham khảo.</summary>
    public bool LowData { get; init; }
}
