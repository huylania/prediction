using Prediction.Core.Entities;

namespace Prediction.Core.Engine.Analysis;

/// <summary>Kết quả kiểm một tích DNA so với số liệu thật.</summary>
public enum DnaCheckStatus
{
    /// <summary>Số liệu ủng hộ tích này.</summary>
    Match = 0,

    /// <summary>Lưng chừng: không ủng hộ rõ cũng không ngược.</summary>
    Borderline = 1,

    /// <summary>Số liệu ngược hẳn với tích này.</summary>
    Contradict = 2,

    /// <summary>Chưa đủ trận có số liệu để kiểm.</summary>
    NoData = 3,

    /// <summary>Không kiểm được bằng tỉ số / thống kê (cần dữ liệu kèo).</summary>
    Untestable = 4,
}

public sealed record DnaTagCheck(DnaTag Tag, DnaCheckStatus Status, string Text);

/// <summary>Một trận đã đá nhìn từ phía một đội (hoặc từ đội nhà khi tính cho giải).</summary>
public sealed record VerifyMatch(
    bool IsHome,
    int GoalsFor,
    int GoalsAgainst,
    int? HalftimeFor = null,
    int? HalftimeAgainst = null,
    int? CornersFor = null,
    int? CornersAgainst = null,
    int? CardsFor = null,
    int? CardsAgainst = null,
    bool? LateGoal = null)
{
    public int TotalGoals => GoalsFor + GoalsAgainst;
}

/// <summary>Một chỉ số tính từ n trận (tỉ lệ % hoặc trung bình).</summary>
public readonly record struct DnaMetric(double? Value, int Sample)
{
    public static DnaMetric Rate(IEnumerable<bool?> values)
    {
        var list = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return list.Count == 0 ? new(null, 0) : new(100.0 * list.Count(v => v) / list.Count, list.Count);
    }

    public static DnaMetric Average(IEnumerable<double?> values)
    {
        var list = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return list.Count == 0 ? new(null, 0) : new(list.Average(), list.Count);
    }
}

/// <summary>Số liệu 1 năm của một đội hoặc một giải, tính từ các trận API-Football đã lưu.</summary>
public sealed class DnaStats
{
    public int Matches { get; init; }
    public DnaMetric Goals { get; init; }
    public DnaMetric Over25 { get; init; }
    public DnaMetric Btts { get; init; }
    public DnaMetric Draw { get; init; }

    /// <summary>Đội: % thắng sân nhà. Giải: % chủ nhà thắng.</summary>
    public DnaMetric HomeWin { get; init; }

    /// <summary>Đội: % thua sân khách.</summary>
    public DnaMetric AwayLoss { get; init; }

    public DnaMetric CornersFor { get; init; }
    public DnaMetric CornersAgainst { get; init; }
    public DnaMetric CornersTotal { get; init; }
    public DnaMetric FirstHalfGoal { get; init; }
    public DnaMetric SecondHalfGoals { get; init; }
    public DnaMetric LateGoal { get; init; }
    public DnaMetric CardsFor { get; init; }
    public DnaMetric CardsTotal { get; init; }
}

/// <summary>
/// Kiểm tích DNA (do người dùng / file CSV gắn) với số liệu thật 1 năm của API-Football. Mỗi tích có ngưỡng "khớp" và
/// ngưỡng "ngược"; ở giữa là lưng chừng. Tích cần dữ liệu kèo (cửa dưới hay ăn, tín kèo trên / bắt nạt đội yếu) không kiểm được.
/// </summary>
public static class DnaVerifier
{
    public const int MinTeamMatches = 10;
    public const int MinLeagueMatches = 40;

    private sealed record Rule(Func<DnaStats, DnaMetric> Metric, int MinSample, Func<double, bool> Good, Func<double, bool> Bad, string Name, bool Percent);

    private static readonly Dictionary<DnaTag, Rule> TeamRules = new()
    {
        [DnaTag.OverGoals] = new(s => s.Over25, MinTeamMatches, v => v >= 55, v => v < 45, "Tài 2.5", true),
        [DnaTag.HomeFortress] = new(s => s.HomeWin, 6, v => v >= 55, v => v < 40, "thắng sân nhà", true),
        [DnaTag.AwayFragile] = new(s => s.AwayLoss, 6, v => v >= 50, v => v < 30, "thua sân khách", true),
        [DnaTag.OverCorners] = new(s => s.CornersFor, 8, v => v >= 5.5, v => v < 4.5, "góc kiếm/trận", false),
        [DnaTag.ConcedesCorners] = new(s => s.CornersAgainst, 8, v => v >= 5.5, v => v < 4.5, "góc chịu/trận", false),
        [DnaTag.Btts] = new(s => s.Btts, MinTeamMatches, v => v >= 55, v => v < 40, "hai đội cùng ghi bàn", true),
        [DnaTag.FastStart] = new(s => s.FirstHalfGoal, 8, v => v >= 72, v => v < 60, "trận có bàn hiệp 1", true),
        [DnaTag.LateGoals] = new(s => s.SecondHalfGoals, 8, v => v >= 1.6, v => v < 1.2, "bàn hiệp 2/trận", false),
        [DnaTag.HeavyCards] = new(s => s.CardsFor, 8, v => v >= 2.4, v => v < 1.8, "thẻ của đội/trận", false),
        [DnaTag.Pragmatic] = new(s => s.Over25, MinTeamMatches, v => v <= 45, v => v > 60, "Tài 2.5", true),
        [DnaTag.UnderGoals] = new(s => s.Over25, MinTeamMatches, v => v <= 45, v => v > 55, "Tài 2.5", true),
        [DnaTag.UnderCorners] = new(s => s.CornersTotal, 8, v => v <= 9.0, v => v > 10.5, "tổng góc/trận", false),
    };

    private static readonly Dictionary<DnaTag, Rule> LeagueRules = new()
    {
        [DnaTag.OverGoals] = new(s => s.Over25, MinLeagueMatches, v => v >= 55, v => v < 48, "Tài 2.5", true),
        [DnaTag.UnderGoals] = new(s => s.Over25, MinLeagueMatches, v => v <= 47, v => v > 53, "Tài 2.5", true),
        [DnaTag.HomeFortress] = new(s => s.HomeWin, MinLeagueMatches, v => v >= 47, v => v < 42, "chủ nhà thắng", true),
        [DnaTag.OverCorners] = new(s => s.CornersTotal, 30, v => v >= 10.0, v => v < 9.3, "góc/trận", false),
        [DnaTag.UnderCorners] = new(s => s.CornersTotal, 30, v => v <= 9.3, v => v > 10.0, "góc/trận", false),
        [DnaTag.LateGoals] = new(s => s.LateGoal, 30, v => v >= 40, v => v < 33, "trận có bàn sau phút 75", true),
        [DnaTag.Pragmatic] = new(s => s.Draw, MinLeagueMatches, v => v >= 27, v => v < 22, "hòa", true),
        [DnaTag.FastStart] = new(s => s.FirstHalfGoal, 30, v => v >= 72, v => v < 65, "trận có bàn hiệp 1", true),
        [DnaTag.HeavyCards] = new(s => s.CardsTotal, 30, v => v >= 4.8, v => v < 4.2, "thẻ/trận", false),
    };

    /// <summary>Số liệu từ các trận nhìn theo phía đội (đội) hoặc theo đội nhà (giải).</summary>
    public static DnaStats Stats(IReadOnlyCollection<VerifyMatch> matches) => new()
    {
        Matches = matches.Count,
        Goals = DnaMetric.Average(matches.Select(m => (double?)m.TotalGoals)),
        Over25 = DnaMetric.Rate(matches.Select(m => (bool?)(m.TotalGoals > 2))),
        Btts = DnaMetric.Rate(matches.Select(m => (bool?)(m.GoalsFor > 0 && m.GoalsAgainst > 0))),
        Draw = DnaMetric.Rate(matches.Select(m => (bool?)(m.GoalsFor == m.GoalsAgainst))),
        HomeWin = DnaMetric.Rate(matches.Where(m => m.IsHome).Select(m => (bool?)(m.GoalsFor > m.GoalsAgainst))),
        AwayLoss = DnaMetric.Rate(matches.Where(m => !m.IsHome).Select(m => (bool?)(m.GoalsFor < m.GoalsAgainst))),
        CornersFor = DnaMetric.Average(matches.Select(m => m.CornersAgainst.HasValue ? (double?)m.CornersFor : null)),
        CornersAgainst = DnaMetric.Average(matches.Select(m => m.CornersFor.HasValue ? (double?)m.CornersAgainst : null)),
        CornersTotal = DnaMetric.Average(matches.Select(m => m.CornersFor + m.CornersAgainst is { } c ? (double?)c : null)),
        FirstHalfGoal = DnaMetric.Rate(matches.Select(m => m.HalftimeFor + m.HalftimeAgainst is { } h ? (bool?)(h > 0) : null)),
        SecondHalfGoals = DnaMetric.Average(matches.Select(m => m.HalftimeFor + m.HalftimeAgainst is { } h ? (double?)(m.TotalGoals - h) : null)),
        LateGoal = DnaMetric.Rate(matches.Select(m => m.LateGoal)),
        CardsFor = DnaMetric.Average(matches.Select(m => m.CardsAgainst.HasValue ? (double?)m.CardsFor : null)),
        CardsTotal = DnaMetric.Average(matches.Select(m => m.CardsFor + m.CardsAgainst is { } c ? (double?)c : null)),
    };

    public static IReadOnlyList<DnaTagCheck> CheckTeam(DnaTag tags, DnaStats stats) => Check(tags, stats, TeamRules, true);

    public static IReadOnlyList<DnaTagCheck> CheckLeague(DnaTag tags, DnaStats stats) => Check(tags, stats, LeagueRules, false);

    /// <summary>Gộp các tích bị số liệu bác (để gỡ).</summary>
    public static DnaTag Contradicted(IEnumerable<DnaTagCheck> checks) =>
        checks.Where(c => c.Status == DnaCheckStatus.Contradict).Aggregate(DnaTag.None, (acc, c) => acc | c.Tag);

    /// <summary>Có bàn sau phút 75 (phút 76 trở đi, gồm bù giờ) từ chuỗi "12H,34A,90+2H"; null nếu có bàn mà thiếu diễn biến.</summary>
    public static bool? LateGoal(int totalGoals, string? timeline)
    {
        if (totalGoals == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(timeline))
        {
            return null;
        }

        var minutes = timeline.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Length > 1 && int.TryParse(p[..^1].Split('+')[0], out var m) ? m + (p.Contains('+') ? 1 : 0) : (int?)null)
            .OfType<int>()
            .ToList();
        return minutes.Count == 0 ? null : minutes.Any(m => m >= 76);
    }

    private static List<DnaTagCheck> Check(DnaTag tags, DnaStats stats, Dictionary<DnaTag, Rule> rules, bool team)
    {
        var result = new List<DnaTagCheck>();
        foreach (var tag in Enum.GetValues<DnaTag>().Where(t => t != DnaTag.None && tags.HasFlag(t)))
        {
            var label = DnaTagText.Label(tag, team);
            if (!rules.TryGetValue(tag, out var rule))
            {
                result.Add(new(tag, DnaCheckStatus.Untestable, $"{label}: cần dữ liệu kèo, không kiểm được bằng tỉ số"));
                continue;
            }

            var metric = rule.Metric(stats);
            if (metric.Value is not { } v || metric.Sample < rule.MinSample)
            {
                result.Add(new(tag, DnaCheckStatus.NoData, $"{label}: chưa đủ số liệu ({metric.Sample} trận, cần ≥ {rule.MinSample})"));
                continue;
            }

            var value = rule.Percent ? $"{v:0}%" : v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            var status = rule.Bad(v) ? DnaCheckStatus.Contradict : rule.Good(v) ? DnaCheckStatus.Match : DnaCheckStatus.Borderline;
            var verdict = status switch
            {
                DnaCheckStatus.Match => "khớp",
                DnaCheckStatus.Contradict => "NGƯỢC",
                _ => "lưng chừng",
            };
            result.Add(new(tag, status, $"{label}: {verdict} — {rule.Name} {value} ({metric.Sample} trận)"));
        }

        return result;
    }
}
