using Microsoft.EntityFrameworkCore;
using Prediction.Core.Engine;
using Prediction.Core.Entities;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

public sealed record SeasonOption(int LeagueId, string LeagueName, int Season, int Finished);

public sealed class BacktestResult
{
    public required BacktestReport Report { get; init; }
    public required ContextSet Contexts { get; init; }
}

/// <summary>Chạy kiểm chứng quy tắc trên các trận đã đá trong CSDL.</summary>
public sealed class BacktestService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    /// <summary>Các giải-mùa có trận đã đá, trong phạm vi giải của quy tắc.</summary>
    public async Task<List<SeasonOption>> SeasonsAsync(int? leagueId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var finished = new[] { "FT", "AET", "PEN" };
        var rows = await db.Fixtures.AsNoTracking()
            .Where(f => (leagueId == null || f.LeagueId == leagueId) && finished.Contains(f.Status))
            .GroupBy(f => new { f.LeagueId, f.Season })
            .Select(g => new { g.Key.LeagueId, g.Key.Season, Count = g.Count() })
            .ToListAsync(ct);
        var names = await db.Leagues.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct);

        return rows
            .Select(r => new SeasonOption(r.LeagueId, names.GetValueOrDefault(r.LeagueId) ?? $"#{r.LeagueId}", r.Season, r.Count))
            .OrderBy(r => r.LeagueName).ThenByDescending(r => r.Season)
            .ToList();
    }

    public async Task<BacktestResult> RunAsync(Rule rule, IReadOnlyCollection<(int LeagueId, int Season)> scope, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var fixtures = new List<Fixture>();
        foreach (var (leagueId, season) in scope)
        {
            var seasonFixtures = await db.Fixtures.AsNoTracking()
                .Where(f => f.LeagueId == leagueId && f.Season == season && f.KickoffUtc < now)
                .ToListAsync(ct);
            fixtures.AddRange(seasonFixtures.Where(f => f.IsFinished));
        }

        var contexts = await ContextLoader.LoadAsync(db, fixtures, now, ct);
        var report = BacktestRunner.Run(rule, contexts.Contexts.Values);
        return new BacktestResult { Report = report, Contexts = contexts };
    }
}
