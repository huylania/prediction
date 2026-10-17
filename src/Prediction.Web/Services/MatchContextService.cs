using Microsoft.EntityFrameworkCore;
using Prediction.Core.Engine;
using Prediction.Core.Engine.Analysis;
using Prediction.Core.Entities;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>Bối cảnh một trận: các mốc kèo, cảnh báo ≥ 0.5, lịch sử kèo, sân, thời tiết, đội hình, thông tin bên lề.</summary>
public sealed class MatchContextView
{
    public required Fixture Fixture { get; init; }
    public required string Home { get; init; }
    public required string Away { get; init; }

    public LinePoint? Open { get; init; }
    public LinePoint? Lineups { get; init; }
    public LinePoint? PreKickoff { get; init; }
    public LinePoint? Current { get; init; }

    public IReadOnlyList<OddsAlert> Alerts { get; init; } = [];

    public IReadOnlyList<HistoryLine> HeadToHead { get; init; } = [];
    public DateTime? HeadToHeadCheckedAtUtc { get; init; }
    public IReadOnlyList<HistoryLine> HomeRecent { get; init; } = [];
    public IReadOnlyList<HistoryLine> AwayRecent { get; init; } = [];

    public Venue? Venue { get; init; }
    public FixtureWeather? Weather { get; init; }
    public IReadOnlyList<string> Conditions { get; init; } = [];
    public FixtureLineup? Lineup { get; init; }
    public MatchSideInfo? SideInfo { get; init; }
}

public sealed class MatchContextService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    public const int HistoryCount = 5;

    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public async Task<MatchContextView?> ForFixtureAsync(int fixtureId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var f = await db.Fixtures.AsNoTracking().FirstOrDefaultAsync(x => x.Id == fixtureId, ct);
        if (f is null)
        {
            return null;
        }

        var now = UtcNow;
        var contexts = await ContextLoader.LoadAsync(db, [f], now, ct);
        var ctx = contexts.Contexts[f.Id];
        var home = contexts.TeamName(f.HomeTeamId);
        var away = contexts.TeamName(f.AwayTeamId);
        var checkpoints = await db.OddsCheckpoints.AsNoTracking().Where(c => c.FixtureId == f.Id).ToListAsync(ct);
        var (open, lineups, pre, current) = Points(f, ctx, checkpoints, now);

        var h2h = await HeadToHeadAsync(db, [f], ct);
        var meetings = h2h.GetValueOrDefault(f.Id) ?? [];
        var recent = await RecentAsync(db, [(f.HomeTeamId, f.KickoffUtc), (f.AwayTeamId, f.KickoffUtc)], ct);

        var alerts = OddsAlerts.Movement(open, lineups, pre, current, home, away);
        alerts.AddRange(OddsAlerts.HeadToHead(meetings, f.HomeTeamId, ctx.Handicap?.Current.Line, ctx.Totals?.Current.Line, home, away));

        var venue = f.VenueId is { } vid ? await db.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vid, ct) : null;
        var weather = await db.FixtureWeathers.AsNoTracking().FirstOrDefaultAsync(w => w.FixtureId == f.Id, ct);
        var side = await db.MatchSideInfos.AsNoTracking().FirstOrDefaultAsync(s => s.FixtureId == f.Id, ct);
        var key = H2HMeeting.Key(f.HomeTeamId, f.AwayTeamId);

        return new MatchContextView
        {
            Fixture = f,
            Home = home,
            Away = away,
            Open = open,
            Lineups = lineups,
            PreKickoff = pre,
            Current = current,
            Alerts = alerts,
            HeadToHead = meetings,
            HeadToHeadCheckedAtUtc = (await db.H2HChecks.AsNoTracking().FirstOrDefaultAsync(c => c.PairKey == key, ct))?.CheckedAtUtc,
            HomeRecent = recent.GetValueOrDefault((f.HomeTeamId, f.KickoffUtc)) ?? [],
            AwayRecent = recent.GetValueOrDefault((f.AwayTeamId, f.KickoffUtc)) ?? [],
            Venue = venue,
            Weather = weather,
            Conditions = MatchConditions.Notes(venue, weather, side?.AltitudeM, side?.Surface),
            Lineup = await db.FixtureLineups.AsNoTracking().FirstOrDefaultAsync(l => l.FixtureId == f.Id, ct),
            SideInfo = side,
        };
    }

    /// <summary>Cảnh báo kèo ≥ 0.5 cho nhiều trận (thẻ trận): biến động giữa các mốc và so với đối đầu.</summary>
    public static async Task<Dictionary<int, IReadOnlyList<OddsAlert>>> AlertsAsync(AppDbContext db, IReadOnlyCollection<Fixture> fixtures, ContextSet contexts, DateTime nowUtc, CancellationToken ct)
    {
        var result = new Dictionary<int, IReadOnlyList<OddsAlert>>();
        if (fixtures.Count == 0)
        {
            return result;
        }

        var ids = fixtures.Select(f => f.Id).ToList();
        var checkpoints = new List<OddsCheckpoint>();
        foreach (var chunk in ids.Chunk(500))
        {
            checkpoints.AddRange(await db.OddsCheckpoints.AsNoTracking().Where(c => chunk.Contains(c.FixtureId)).ToListAsync(ct));
        }

        var byFixture = checkpoints.ToLookup(c => c.FixtureId);
        var h2h = await HeadToHeadAsync(db, fixtures, ct);
        foreach (var f in fixtures)
        {
            if (!contexts.Contexts.TryGetValue(f.Id, out var ctx))
            {
                continue;
            }

            var home = contexts.TeamName(f.HomeTeamId);
            var away = contexts.TeamName(f.AwayTeamId);
            var (open, lineups, pre, current) = Points(f, ctx, byFixture[f.Id].ToList(), nowUtc);
            var alerts = OddsAlerts.Movement(open, lineups, pre, current, home, away);
            alerts.AddRange(OddsAlerts.HeadToHead(h2h.GetValueOrDefault(f.Id) ?? [], f.HomeTeamId, ctx.Handicap?.Current.Line, ctx.Totals?.Current.Line, home, away));
            if (alerts.Count > 0)
            {
                result[f.Id] = alerts;
            }
        }

        return result;
    }

    private static (LinePoint? Open, LinePoint? Lineups, LinePoint? Pre, LinePoint? Current) Points(Fixture f, MatchContext ctx, List<OddsCheckpoint> checkpoints, DateTime now)
    {
        var started = f.KickoffUtc <= now || !FixtureStatus.IsUpcoming(f.Status);
        var open = LinePoint.FromMovements("Mở kèo", ctx.Handicap, ctx.Totals, open: true);
        var current = LinePoint.FromMovements(started ? "Kèo đóng" : "Hiện tại", ctx.Handicap, ctx.Totals, open: false);
        var lineups = checkpoints.FirstOrDefault(c => c.Kind == OddsCheckpointKind.Lineups) is { } l ? LinePoint.FromCheckpoint("Khi có đội hình", l) : null;
        var pre = checkpoints.FirstOrDefault(c => c.Kind == OddsCheckpointKind.PreKickoff) is { } p ? LinePoint.FromCheckpoint("5 phút trước trận", p) : null;
        return (open, lineups, pre, current);
    }

    /// <summary>5 lần đối đầu gần nhất (trước giờ bóng lăn) của từng trận: từ API /headtohead và các trận trong CSDL.</summary>
    public static async Task<Dictionary<int, IReadOnlyList<HistoryLine>>> HeadToHeadAsync(AppDbContext db, IReadOnlyCollection<Fixture> fixtures, CancellationToken ct)
    {
        var keys = fixtures.Select(f => H2HMeeting.Key(f.HomeTeamId, f.AwayTeamId)).Distinct().ToList();
        var meetings = new List<H2HMeeting>();
        foreach (var chunk in keys.Chunk(300))
        {
            meetings.AddRange(await db.H2HMeetings.AsNoTracking().Where(m => chunk.Contains(m.PairKey)).ToListAsync(ct));
        }

        // Trận giữa hai đội có sẵn trong CSDL (khi chưa lấy /headtohead).
        var teamIds = fixtures.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().ToList();
        var minKickoff = fixtures.Min(f => f.KickoffUtc).AddYears(-4);
        var local = await db.Fixtures.AsNoTracking()
            .Where(x => teamIds.Contains(x.HomeTeamId) && teamIds.Contains(x.AwayTeamId) && x.KickoffUtc >= minKickoff
                && x.HomeGoals != null && (x.Status == "FT" || x.Status == "AET" || x.Status == "PEN")
                && (x.League!.IsTracked || !x.League.LiveScan))
            .Select(x => new { x.Id, x.KickoffUtc, League = x.League!.Name, x.HomeTeamId, x.AwayTeamId, x.HomeGoals, x.AwayGoals })
            .ToListAsync(ct);
        foreach (var x in local)
        {
            meetings.Add(new H2HMeeting
            {
                FixtureId = x.Id,
                PairKey = H2HMeeting.Key(x.HomeTeamId, x.AwayTeamId),
                KickoffUtc = x.KickoffUtc,
                LeagueName = x.League,
                HomeTeamId = x.HomeTeamId,
                AwayTeamId = x.AwayTeamId,
                HomeGoals = x.HomeGoals,
                AwayGoals = x.AwayGoals,
            });
        }

        var byPair = meetings.DistinctBy(m => m.FixtureId).ToLookup(m => m.PairKey);
        var names = await NamesAsync(db, teamIds, ct);
        var wanted = new Dictionary<int, List<H2HMeeting>>();
        foreach (var f in fixtures)
        {
            wanted[f.Id] = byPair[H2HMeeting.Key(f.HomeTeamId, f.AwayTeamId)]
                .Where(m => m.KickoffUtc < f.KickoffUtc && m.FixtureId != f.Id)
                .OrderByDescending(m => m.KickoffUtc)
                .Take(HistoryCount)
                .ToList();
        }

        var lines = await LinesAsync(db, wanted.Values.SelectMany(m => m).Select(m => m.FixtureId).Distinct().ToList(), ct);
        return wanted.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<HistoryLine>)kv.Value.Select(m => ToLine(m.FixtureId, m.KickoffUtc, m.LeagueName, m.HomeTeamId, m.AwayTeamId, m.HomeGoals, m.AwayGoals, names, lines)).ToList());
    }

    /// <summary>5 trận gần nhất của từng đội trước một thời điểm (mọi giải trong CSDL), kèm kèo.</summary>
    public static async Task<Dictionary<(int TeamId, DateTime Before), IReadOnlyList<HistoryLine>>> RecentAsync(AppDbContext db, IReadOnlyCollection<(int TeamId, DateTime Before)> requests, CancellationToken ct)
    {
        var result = new Dictionary<(int, DateTime), IReadOnlyList<HistoryLine>>();
        var rows = new Dictionary<(int, DateTime), List<Fixture>>();
        foreach (var (team, before) in requests.Distinct())
        {
            rows[(team, before)] = await db.Fixtures.AsNoTracking().Include(x => x.League)
                .Where(x => (x.HomeTeamId == team || x.AwayTeamId == team) && x.KickoffUtc < before
                    && x.HomeGoals != null && (x.Status == "FT" || x.Status == "AET" || x.Status == "PEN")
                    && (x.League!.IsTracked || !x.League.LiveScan))
                .OrderByDescending(x => x.KickoffUtc)
                .Take(HistoryCount)
                .ToListAsync(ct);
        }

        var all = rows.Values.SelectMany(r => r).ToList();
        var names = await NamesAsync(db, all.SelectMany(x => new[] { x.HomeTeamId, x.AwayTeamId }).Distinct().ToList(), ct);
        var lines = await LinesAsync(db, all.Select(x => x.Id).Distinct().ToList(), ct);
        foreach (var (key, list) in rows)
        {
            result[key] = list.Select(x => ToLine(x.Id, x.KickoffUtc, x.League?.Name, x.HomeTeamId, x.AwayTeamId, x.HomeGoals, x.AwayGoals, names, lines)).ToList();
        }

        return result;
    }

    private static HistoryLine ToLine(int id, DateTime kickoff, string? league, int home, int away, int? hg, int? ag,
        Dictionary<int, string> names, Dictionary<int, ResolvedLine> lines)
    {
        var line = lines.GetValueOrDefault(id);
        return new HistoryLine(id, kickoff, league, home, away, names.GetValueOrDefault(home) ?? $"#{home}", names.GetValueOrDefault(away) ?? $"#{away}",
            hg, ag, line?.Handicap, line?.Total, line?.Origin ?? LineOrigin.None, line?.Note);
    }

    private static async Task<Dictionary<int, string>> NamesAsync(AppDbContext db, List<int> ids, CancellationToken ct)
    {
        var names = new Dictionary<int, string>();
        foreach (var chunk in ids.Chunk(500))
        {
            foreach (var t in await db.Teams.AsNoTracking().Where(t => chunk.Contains(t.Id)).Select(t => new { t.Id, t.Name }).ToListAsync(ct))
            {
                names[t.Id] = t.Name;
            }
        }

        return names;
    }

    public sealed record ResolvedLine(decimal? Handicap, decimal? Total, LineOrigin Origin, string? Note);

    /// <summary>Kèo đóng của các trận cũ: website tự ghi trước, rồi nhập tay, rồi AI tra cứu.</summary>
    public static async Task<Dictionary<int, ResolvedLine>> LinesAsync(AppDbContext db, List<int> fixtureIds, CancellationToken ct)
    {
        var recorded = new Dictionary<(int, Market), decimal>();
        var manual = new Dictionary<int, ClosingLine>();
        foreach (var chunk in fixtureIds.Chunk(500))
        {
            var last = await db.OddsSnapshots.AsNoTracking()
                .Where(s => chunk.Contains(s.FixtureId) && (s.Market == Market.AsianHandicap || s.Market == Market.OverUnder) && s.FetchedAtUtc <= s.Fixture!.KickoffUtc)
                .GroupBy(s => new { s.FixtureId, s.Market })
                .Select(g => g.OrderByDescending(s => s.FetchedAtUtc).ThenByDescending(s => s.Id).Select(s => new { s.FixtureId, s.Market, s.Line }).First())
                .ToListAsync(ct);
            foreach (var l in last)
            {
                recorded[(l.FixtureId, l.Market)] = l.Line;
            }

            foreach (var c in await db.ClosingLines.AsNoTracking().Where(c => chunk.Contains(c.FixtureId)).ToListAsync(ct))
            {
                manual[c.FixtureId] = c;
            }
        }

        var result = new Dictionary<int, ResolvedLine>();
        foreach (var id in fixtureIds)
        {
            var hasAh = recorded.TryGetValue((id, Market.AsianHandicap), out var ah);
            var hasOu = recorded.TryGetValue((id, Market.OverUnder), out var ou);
            var c = manual.GetValueOrDefault(id);
            if (!hasAh && !hasOu && c is null)
            {
                continue;
            }

            var origin = hasAh || hasOu ? LineOrigin.Recorded : c!.Source == LineSource.Manual ? LineOrigin.Manual : LineOrigin.Ai;
            result[id] = new ResolvedLine(hasAh ? ah : c?.HandicapLine, hasOu ? ou : c?.TotalLine, origin, hasAh || hasOu ? null : c?.Note);
        }

        return result;
    }

    /// <summary>Lưu kèo đóng nhập tay cho một trận cũ (ghi đè kèo AI).</summary>
    public async Task SaveManualLineAsync(int fixtureId, decimal? handicap, decimal? total, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.ClosingLines.FindAsync([fixtureId], ct);
        if (handicap is null && total is null)
        {
            if (row is not null)
            {
                db.ClosingLines.Remove(row);
            }
        }
        else
        {
            if (row is null)
            {
                row = new ClosingLine { FixtureId = fixtureId };
                db.ClosingLines.Add(row);
            }

            row.HandicapLine = handicap;
            row.TotalLine = total;
            row.Source = LineSource.Manual;
            row.Note = null;
            row.UpdatedAtUtc = UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
