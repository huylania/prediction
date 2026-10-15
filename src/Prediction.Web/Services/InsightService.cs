using Microsoft.EntityFrameworkCore;
using Prediction.Core.Engine;
using Prediction.Core.Entities;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>Một trận kèm ngữ cảnh và tư vấn tổng hợp, để hiển thị.</summary>
public sealed class MatchInsight
{
    public required Fixture Fixture { get; init; }
    public required League? League { get; init; }
    public required MatchContext Context { get; init; }
    public required string Home { get; init; }
    public required string Away { get; init; }
    public required MatchAdvice Advice { get; init; }
    public required IReadOnlyList<OddsSnapshot> Snapshots { get; init; }
    public Team? HomeTeam { get; init; }
    public Team? AwayTeam { get; init; }

    /// <summary>Khuyến nghị tốt nhất của phân tích chuyên sâu (đã lưu), nếu có.</summary>
    public AnalysisPick? Analysis { get; init; }

    public bool HasPicks => Advice.Picks.Count > 0;
}

/// <summary>Dựng dữ liệu tư vấn cho các trang Tổng quan, Trận đấu, Biến động kèo.</summary>
public sealed class InsightService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public async Task<List<MatchInsight>> ForWindowAsync(DateTime fromUtc, DateTime toUtc, int? leagueId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fixtures = await db.Fixtures.AsNoTracking()
            .Where(f => f.KickoffUtc >= fromUtc && f.KickoffUtc < toUtc && (leagueId == null || f.LeagueId == leagueId))
            .OrderBy(f => f.KickoffUtc).ThenBy(f => f.Id)
            .ToListAsync(ct);
        return await BuildAsync(db, fixtures, ct);
    }

    public async Task<MatchInsight?> ForFixtureAsync(int fixtureId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fixture = await db.Fixtures.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fixtureId, ct);
        return fixture is null ? null : (await BuildAsync(db, [fixture], ct)).FirstOrDefault();
    }

    /// <summary>Xét một quy tắc (có thể chưa lưu) trên các trận sắp đá — dùng cho "Xem trước" khi sửa quy tắc.</summary>
    public async Task<List<(MatchInsight Match, Pick Pick)>> PreviewAsync(Rule rule, CancellationToken ct = default)
    {
        var now = UtcNow;
        var matches = await ForWindowAsync(now, now + SuggestionService.Lookahead, rule.LeagueId, ct);
        return matches
            .Where(m => FixtureStatus.IsUpcoming(m.Fixture.Status))
            .Select(m => (m, RuleEvaluator.Evaluate(rule, m.Context)))
            .Where(x => x.Item2 is not null)
            .Select(x => (x.m, x.Item2!))
            .ToList();
    }

    private async Task<List<MatchInsight>> BuildAsync(AppDbContext db, List<Fixture> fixtures, CancellationToken ct)
    {
        if (fixtures.Count == 0)
        {
            return [];
        }

        var now = UtcNow;
        var contexts = await ContextLoader.LoadAsync(db, fixtures, now, ct);
        var rules = await db.Rules.AsNoTracking().Where(r => r.IsActive).ToListAsync(ct);
        var records = await ContextLoader.LoadRecordsAsync(db, ct);
        var leagueIds = fixtures.Select(f => f.LeagueId).Distinct().ToList();
        var leagues = await db.Leagues.AsNoTracking().Where(l => leagueIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var analyses = await AnalysisService.BestPicksAsync(db, fixtures.Select(f => f.Id).ToList(), ct);

        var result = new List<MatchInsight>(fixtures.Count);
        foreach (var fixture in fixtures)
        {
            var ctx = contexts.Contexts[fixture.Id];
            var home = contexts.TeamName(fixture.HomeTeamId);
            var away = contexts.TeamName(fixture.AwayTeamId);
            var picks = rules
                .Select(r => (Rule: r, Pick: RuleEvaluator.Evaluate(r, ctx)))
                .Where(x => x.Pick is not null)
                .Select(x => new RulePick(x.Rule, x.Pick!, records.GetValueOrDefault(x.Rule.Id) ?? RuleRecord.Empty));

            result.Add(new MatchInsight
            {
                Fixture = fixture,
                League = leagues.GetValueOrDefault(fixture.LeagueId),
                Context = ctx,
                Home = home,
                Away = away,
                HomeTeam = contexts.Teams.GetValueOrDefault(fixture.HomeTeamId),
                AwayTeam = contexts.Teams.GetValueOrDefault(fixture.AwayTeamId),
                Advice = MatchAdvisor.Advise(ctx, picks, home, away),
                Snapshots = contexts.Snapshots.GetValueOrDefault(fixture.Id) ?? [],
                Analysis = analyses.GetValueOrDefault(fixture.Id),
            });
        }

        return result;
    }
}
