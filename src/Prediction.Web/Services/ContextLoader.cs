using Microsoft.EntityFrameworkCore;
using Prediction.Core.Engine;
using Prediction.Core.Entities;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>Kết quả dựng ngữ cảnh: ngữ cảnh theo trận + tên đội để hiển thị.</summary>
public sealed class ContextSet
{
    public Dictionary<int, MatchContext> Contexts { get; } = [];

    public Dictionary<int, Team> Teams { get; } = [];

    public Dictionary<int, List<OddsSnapshot>> Snapshots { get; } = [];

    public string TeamName(int id) => Teams.TryGetValue(id, out var t) ? t.Name : $"#{id}";
}

/// <summary>
/// Dựng <see cref="MatchContext"/> cho các trận: BXH/phong độ trước trận, biến động kèo đến
/// min(giờ bóng lăn, thời điểm xét) và tư vấn API.
/// </summary>
public static class ContextLoader
{
    private const int ChunkSize = 500;

    public static async Task<ContextSet> LoadAsync(AppDbContext db, IReadOnlyCollection<Fixture> targets, DateTime nowUtc, CancellationToken ct)
    {
        var set = new ContextSet();
        if (targets.Count == 0)
        {
            return set;
        }

        // BXH được tính trên toàn bộ trận của từng giải-mùa có liên quan.
        var timelines = new Dictionary<(int, int), Dictionary<int, (TeamSnapshot Home, TeamSnapshot Away)>>();
        foreach (var key in targets.Select(f => (f.LeagueId, f.Season)).Distinct())
        {
            var season = await db.Fixtures.AsNoTracking()
                .Where(f => f.LeagueId == key.LeagueId && f.Season == key.Season)
                .ToListAsync(ct);
            timelines[key] = StandingsTimeline.Build(season);
        }

        var ids = targets.Select(f => f.Id).ToList();
        foreach (var chunk in ids.Chunk(ChunkSize))
        {
            var snapshots = await db.OddsSnapshots.AsNoTracking().Where(s => chunk.Contains(s.FixtureId)).ToListAsync(ct);
            foreach (var group in snapshots.GroupBy(s => s.FixtureId))
            {
                set.Snapshots[group.Key] = group.OrderBy(s => s.FetchedAtUtc).ThenBy(s => s.Id).ToList();
            }
        }

        var advice = new Dictionary<int, ApiAdvice>();
        foreach (var chunk in ids.Chunk(ChunkSize))
        {
            foreach (var a in await db.ApiAdvices.AsNoTracking().Where(a => chunk.Contains(a.FixtureId)).ToListAsync(ct))
            {
                advice[a.FixtureId] = a;
            }
        }

        var teamIds = targets.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().ToList();
        foreach (var chunk in teamIds.Chunk(ChunkSize))
        {
            foreach (var t in await db.Teams.AsNoTracking().Where(t => chunk.Contains(t.Id)).ToListAsync(ct))
            {
                set.Teams[t.Id] = t;
            }
        }

        foreach (var fixture in targets)
        {
            timelines[(fixture.LeagueId, fixture.Season)].TryGetValue(fixture.Id, out var standing);
            var cutoff = fixture.KickoffUtc < nowUtc ? fixture.KickoffUtc : nowUtc;
            var snapshots = set.Snapshots.GetValueOrDefault(fixture.Id) ?? [];
            set.Contexts[fixture.Id] = new MatchContext
            {
                Fixture = fixture,
                Home = standing.Home,
                Away = standing.Away,
                Handicap = MarketMovement.Build(snapshots, Market.AsianHandicap, cutoff),
                Totals = MarketMovement.Build(snapshots, Market.OverUnder, cutoff),
                Advice = advice.GetValueOrDefault(fixture.Id),
            };
        }

        return set;
    }

    /// <summary>Thành tích thực tế của các quy tắc từ những gợi ý đã chấm.</summary>
    public static async Task<Dictionary<int, RuleRecord>> LoadRecordsAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.Suggestions.AsNoTracking()
            .Where(s => s.Outcome != BetOutcome.Pending && s.Outcome != BetOutcome.Void)
            .GroupBy(s => s.RuleId)
            .Select(g => new
            {
                RuleId = g.Key,
                Settled = g.Count(),
                Wins = g.Count(x => x.Outcome == BetOutcome.Win),
                HalfWins = g.Count(x => x.Outcome == BetOutcome.HalfWin),
                Pushes = g.Count(x => x.Outcome == BetOutcome.Push),
                HalfLosses = g.Count(x => x.Outcome == BetOutcome.HalfLoss),
                Losses = g.Count(x => x.Outcome == BetOutcome.Loss),
                Profit = g.Sum(x => x.Profit ?? 0),
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.RuleId,
            r => new RuleRecord(r.Settled, r.Wins, r.HalfWins, r.Pushes, r.HalfLosses, r.Losses, r.Profit));
    }
}
