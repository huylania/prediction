using Prediction.Core.Entities;

namespace Prediction.Core.Engine.Analysis;

/// <summary>Tính mẫu phong độ 10 trận (hoặc giai đoạn HLV/tân binh mới) và tình trạng nhân sự của một đội.</summary>
public static class TeamFormBuilder
{
    public const int SampleSize = 10;

    /// <summary>
    /// Thay đổi nhân sự đáng xét nhất tại thời điểm trận đấu: ưu tiên thay đổi đang trong 1–3 trận đầu, sau đó thay đổi mới nhất.
    /// Trả về null nếu không có, hoặc đã qua 10 trận (không còn ảnh hưởng mẫu dữ liệu).
    /// </summary>
    public static RegimeStatus? Regime(IEnumerable<TeamChange> changes, IReadOnlyList<TeamMatch> history, DateTime kickoffUtc)
    {
        var statuses = changes
            .Where(c => !c.IsIgnored && c.StartDate <= kickoffUtc && c.StartDate >= kickoffUtc.AddDays(-400))
            .Select(c => new RegimeStatus(c.Kind, c.PersonName, c.StartDate,
                history.Count(m => m.KickoffUtc >= c.StartDate && m.KickoffUtc < kickoffUtc)))
            .Where(s => s.MatchesUnder < SampleSize)
            .ToList();

        return statuses.Where(s => s.InBuffer).OrderByDescending(s => s.StartDate).FirstOrDefault()
            ?? statuses.OrderByDescending(s => s.StartDate).FirstOrDefault();
    }

    /// <param name="history">Các trận đã đá, mới nhất trước.</param>
    /// <param name="upcomingIsHome">Trận sắp đá đội này đá sân nhà (để tách thống kê góc theo sân).</param>
    public static TeamForm Build(IReadOnlyList<TeamMatch> history, bool upcomingIsHome, RegimeStatus? regime)
    {
        var newSample = regime is { UsesNewSample: true };
        var pool = newSample ? history.Where(m => m.KickoffUtc >= regime!.StartDate).ToList() : history.ToList();
        var sample = pool.Take(SampleSize).ToList();
        var n = sample.Count;

        var label = newSample
            ? $"{n} trận dưới thời {(regime!.Kind == TeamChangeKind.NewManager ? "HLV" : "tân binh")} {regime.PersonName}"
            : n == 0 ? "chưa có dữ liệu" : $"{n} trận gần nhất";

        var corners = sample.Where(m => m.CornersFor.HasValue && m.CornersAgainst.HasValue).ToList();
        var venue = pool.Where(m => m.IsHome == upcomingIsHome && m.CornersFor.HasValue && m.CornersAgainst.HasValue).Take(SampleSize).ToList();
        var xg = sample.Where(m => m.XgFor.HasValue && m.XgAgainst.HasValue).ToList();
        var shots = sample.Where(m => m.ShotsFor.HasValue).ToList();
        var cards = sample.Where(m => m.CardsFor.HasValue && m.CardsAgainst.HasValue).ToList();

        var ats = sample
            .Where(m => m.ClosingHandicap.HasValue)
            .Select(m => Settlement.Settle(PickSide.Home, m.ClosingHandicap!.Value, 2m, m.GoalsFor, m.GoalsAgainst).Outcome)
            .Where(o => o != BetOutcome.Push)
            .ToList();

        return new TeamForm
        {
            Matches = n,
            SampleLabel = label,
            Form = new string(sample.Take(5).Select(m => m.Result).ToArray()),
            GoalsFor = Avg(sample, m => m.GoalsFor) ?? 0,
            GoalsAgainst = Avg(sample, m => m.GoalsAgainst) ?? 0,
            Over25Rate = Rate(sample, m => m.TotalGoals > 2),
            DrawRate = Rate(sample, m => m.GoalsFor == m.GoalsAgainst),
            WinRate = Rate(sample, m => m.GoalsFor > m.GoalsAgainst),
            CornerMatches = corners.Count,
            CornersFor = Avg(corners, m => m.CornersFor!.Value),
            CornersAgainst = Avg(corners, m => m.CornersAgainst!.Value),
            Over95CornersRate = corners.Count == 0 ? null : Rate(corners, m => m.CornersFor + m.CornersAgainst > 9),
            VenueCornerMatches = venue.Count,
            VenueCornersFor = Avg(venue, m => m.CornersFor!.Value),
            VenueCornersAgainst = Avg(venue, m => m.CornersAgainst!.Value),
            XgMatches = xg.Count,
            XgFor = Avg(xg, m => (double)m.XgFor!.Value),
            XgAgainst = Avg(xg, m => (double)m.XgAgainst!.Value),
            ShotsFor = Avg(shots, m => m.ShotsFor!.Value),
            CardsFor = Avg(cards, m => m.CardsFor!.Value),
            CardsAgainst = Avg(cards, m => m.CardsAgainst!.Value),
            AtsMatches = ats.Count,
            AtsRate = ats.Count == 0 ? null : ats.Count(o => o is BetOutcome.Win or BetOutcome.HalfWin) / (double)ats.Count,
        };
    }

    private static double? Avg<T>(IReadOnlyCollection<T> items, Func<T, double> value) =>
        items.Count == 0 ? null : items.Average(value);

    private static double Rate<T>(IReadOnlyCollection<T> items, Func<T, bool> predicate) =>
        items.Count == 0 ? 0 : items.Count(predicate) / (double)items.Count;
}
