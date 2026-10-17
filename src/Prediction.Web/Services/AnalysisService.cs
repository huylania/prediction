using Microsoft.EntityFrameworkCore;
using Prediction.Core.Engine;
using Prediction.Core.Engine.Analysis;
using Prediction.Core.Entities;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>Kết quả phân tích chuyên sâu của một trận, kèm dữ liệu đầu vào để hiển thị.</summary>
public sealed class MatchAnalysisResult
{
    public required Fixture Fixture { get; init; }
    public required League? League { get; init; }
    public required string Home { get; init; }
    public required string Away { get; init; }
    public Team? HomeTeam { get; init; }
    public Team? AwayTeam { get; init; }
    public required AnalysisInput Input { get; init; }
    public required MatchAnalysis Analysis { get; init; }

    /// <summary>Cảnh báo kèo chấp/tài xỉu dịch ≥ 0.5 hoặc lệch lịch sử đối đầu.</summary>
    public IReadOnlyList<OddsAlert> OddsAlerts { get; init; } = [];
}

/// <summary>
/// Dựng dữ liệu đầu vào (phong độ, thống kê góc/thẻ, DNA, nhân sự, chấn thương, kèo) và chạy <see cref="MatchAnalyzer"/>;
/// lưu khuyến nghị để chốt theo kèo trước giờ bóng lăn và chấm thành tích.
/// </summary>
public sealed class AnalysisService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    /// <summary>Phân tích các trận sắp đá trong khoảng này.</summary>
    public static readonly TimeSpan Lookahead = TimeSpan.FromDays(3);

    private const int HistoryPerTeam = 20;
    private const int Chunk = 500;

    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public async Task<MatchAnalysisResult?> ForFixtureAsync(int fixtureId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fixture = await db.Fixtures.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fixtureId, ct);
        return fixture is null ? null : (await AnalyzeAsync(db, [fixture], ct)).GetValueOrDefault(fixture.Id);
    }

    /// <summary>Phân tích các trận sắp đá (chưa bóng lăn) trong <paramref name="window"/> tới.</summary>
    public async Task<List<MatchAnalysisResult>> UpcomingAsync(TimeSpan window, int? leagueId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = UtcNow;
        var until = now + window;
        var fixtures = await db.Fixtures.AsNoTracking()
            .Where(f => f.KickoffUtc > now && f.KickoffUtc <= until && (f.Status == "NS" || f.Status == "TBD")
                && f.League!.IsTracked && (leagueId == null || f.LeagueId == leagueId))
            .OrderBy(f => f.KickoffUtc).ThenBy(f => f.Id)
            .ToListAsync(ct);
        var results = await AnalyzeAsync(db, fixtures, ct);
        return fixtures.Select(f => results[f.Id]).ToList();
    }

    public async Task<Dictionary<int, MatchAnalysisResult>> AnalyzeAsync(AppDbContext db, IReadOnlyCollection<Fixture> fixtures, CancellationToken ct)
    {
        var result = new Dictionary<int, MatchAnalysisResult>();
        if (fixtures.Count == 0)
        {
            return result;
        }

        var now = UtcNow;
        var contexts = await ContextLoader.LoadAsync(db, fixtures, now, ct);
        var ids = fixtures.Select(f => f.Id).ToList();
        var leagueIds = fixtures.Select(f => f.LeagueId).Distinct().ToList();
        var teamIds = fixtures.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().ToList();

        var leagues = await db.Leagues.AsNoTracking().Where(l => leagueIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var leagueDna = await db.LeagueDnas.AsNoTracking().Where(d => leagueIds.Contains(d.LeagueId)).ToDictionaryAsync(d => d.LeagueId, ct);
        var teamDna = await db.TeamDnas.AsNoTracking().Where(d => teamIds.Contains(d.TeamId)).ToDictionaryAsync(d => d.TeamId, ct);
        var changes = (await db.TeamChanges.AsNoTracking().Where(c => teamIds.Contains(c.TeamId)).ToListAsync(ct))
            .ToLookup(c => c.TeamId);
        var injuries = (await db.FixtureInjuries.AsNoTracking().Where(i => ids.Contains(i.FixtureId)).ToListAsync(ct))
            .ToLookup(i => i.FixtureId);
        var ruleSides = (await db.Suggestions.AsNoTracking()
                .Where(s => ids.Contains(s.FixtureId) && (s.CurrentlyMatches || s.IsFrozen) && s.Rule!.IsActive)
                .Select(s => new { s.FixtureId, s.Side })
                .ToListAsync(ct))
            .ToLookup(s => s.FixtureId, s => s.Side);

        var venueIds = fixtures.Where(f => f.VenueId.HasValue).Select(f => f.VenueId!.Value).Distinct().ToList();
        var elevations = await db.Venues.AsNoTracking().Where(v => venueIds.Contains(v.Id) && v.ElevationM != null)
            .ToDictionaryAsync(v => v.Id, v => v.ElevationM, ct);
        var aiAltitude = await db.MatchSideInfos.AsNoTracking().Where(s => ids.Contains(s.FixtureId) && s.AltitudeM != null)
            .ToDictionaryAsync(s => s.FixtureId, s => s.AltitudeM, ct);

        var oddsAlerts = await MatchContextService.AlertsAsync(db, fixtures, contexts, now, ct);
        var history = await LoadHistoryAsync(db, teamIds, fixtures.Min(f => f.KickoffUtc).AddDays(-400), fixtures.Max(f => f.KickoffUtc), ct);
        var baselines = new Dictionary<(int, int), LeagueBaseline>();
        foreach (var key in fixtures.Select(f => (f.LeagueId, f.Season)).Distinct())
        {
            // CSDL chưa đủ trận thì dùng TB trong hồ sơ 1 năm của giải (file DNA giải) trước số mặc định.
            var profile = leagueDna.GetValueOrDefault(key.LeagueId);
            baselines[key] = await BaselineAsync(db, key.LeagueId, key.Season, ct) with
            {
                ProfileGoals = (double?)profile?.AvgGoals,
                ProfileCorners = (double?)profile?.AvgCorners,
                ProfileCards = (double?)profile?.AvgCards,
            };
        }

        foreach (var fixture in fixtures)
        {
            var ctx = contexts.Contexts[fixture.Id];
            var league = leagues.GetValueOrDefault(fixture.LeagueId);
            var home = contexts.TeamName(fixture.HomeTeamId);
            var away = contexts.TeamName(fixture.AwayTeamId);
            var input = new AnalysisInput
            {
                Fixture = fixture,
                HomeName = home,
                AwayName = away,
                LeagueName = league?.Name,
                LeagueType = league?.Type,
                LeagueDna = leagueDna.GetValueOrDefault(fixture.LeagueId),
                HomeDna = teamDna.GetValueOrDefault(fixture.HomeTeamId),
                AwayDna = teamDna.GetValueOrDefault(fixture.AwayTeamId),
                HomeStanding = ctx.Home,
                AwayStanding = ctx.Away,
                TableSize = contexts.TableSizes.GetValueOrDefault((fixture.LeagueId, fixture.Season)),
                HomeHistory = TeamHistory(history, fixture.HomeTeamId, fixture.KickoffUtc),
                AwayHistory = TeamHistory(history, fixture.AwayTeamId, fixture.KickoffUtc),
                League = baselines[(fixture.LeagueId, fixture.Season)],
                Handicap = ctx.Handicap,
                Totals = ctx.Totals,
                Corners = ctx.Corners,
                CornersHandicap = ctx.CornersHandicap,
                Cards = ctx.Cards,
                HomeChanges = changes[fixture.HomeTeamId].ToList(),
                AwayChanges = changes[fixture.AwayTeamId].ToList(),
                Injuries = injuries[fixture.Id].ToList(),
                Advice = ctx.Advice,
                RuleSides = ruleSides[fixture.Id].Distinct().ToList(),
                VenueElevationM = (fixture.VenueId is { } vid ? elevations.GetValueOrDefault(vid) : null) ?? aiAltitude.GetValueOrDefault(fixture.Id),
            };

            result[fixture.Id] = new MatchAnalysisResult
            {
                Fixture = fixture,
                League = league,
                Home = home,
                Away = away,
                HomeTeam = contexts.Teams.GetValueOrDefault(fixture.HomeTeamId),
                AwayTeam = contexts.Teams.GetValueOrDefault(fixture.AwayTeamId),
                Input = input,
                Analysis = MatchAnalyzer.Analyze(input),
                OddsAlerts = oddsAlerts.GetValueOrDefault(fixture.Id) ?? [],
            };
        }

        return result;
    }

    /// <summary>Các trận đã đá của các đội (mọi giải có trong CSDL), kèm thống kê và kèo chấp đóng.</summary>
    private static async Task<List<HistoryRow>> LoadHistoryAsync(AppDbContext db, List<int> teamIds, DateTime fromUtc, DateTime untilUtc, CancellationToken ct)
    {
        var rows = new List<HistoryRow>();
        foreach (var chunk in teamIds.Chunk(200))
        {
            rows.AddRange(await db.Fixtures.AsNoTracking()
                .Where(f => (chunk.Contains(f.HomeTeamId) || chunk.Contains(f.AwayTeamId))
                    && f.KickoffUtc >= fromUtc && f.KickoffUtc < untilUtc
                    && f.HomeGoals != null && f.AwayGoals != null
                    && (f.Status == "FT" || f.Status == "AET" || f.Status == "PEN"))
                .Select(f => new HistoryRow(f.Id, f.KickoffUtc, f.HomeTeamId, f.AwayTeamId, f.HomeGoals!.Value, f.AwayGoals!.Value))
                .ToListAsync(ct));
        }

        rows = rows.DistinctBy(r => r.Id).ToList();
        var ids = rows.Select(r => r.Id).ToList();
        var stats = new Dictionary<int, FixtureStats>();
        var closing = new Dictionary<int, decimal>();
        foreach (var chunk in ids.Chunk(Chunk))
        {
            foreach (var s in await db.FixtureStats.AsNoTracking().Where(s => chunk.Contains(s.FixtureId)).ToListAsync(ct))
            {
                stats[s.FixtureId] = s;
            }

            var lines = await db.OddsSnapshots.AsNoTracking()
                .Where(s => chunk.Contains(s.FixtureId) && s.Market == Market.AsianHandicap && s.FetchedAtUtc <= s.Fixture!.KickoffUtc)
                .GroupBy(s => s.FixtureId)
                .Select(g => g.OrderByDescending(s => s.FetchedAtUtc).ThenByDescending(s => s.Id).Select(s => new { s.FixtureId, s.Line }).First())
                .ToListAsync(ct);
            foreach (var l in lines)
            {
                closing[l.FixtureId] = l.Line;
            }
        }

        foreach (var row in rows)
        {
            row.Stats = stats.GetValueOrDefault(row.Id);
            row.ClosingHomeLine = closing.TryGetValue(row.Id, out var line) ? line : null;
        }

        return rows;
    }

    private static List<TeamMatch> TeamHistory(List<HistoryRow> rows, int teamId, DateTime beforeUtc) =>
        rows.Where(r => (r.HomeTeamId == teamId || r.AwayTeamId == teamId) && r.KickoffUtc < beforeUtc)
            .OrderByDescending(r => r.KickoffUtc)
            .Take(HistoryPerTeam)
            .Select(r => r.For(teamId))
            .ToList();

    /// <summary>Trung bình bàn / góc / thẻ của giải ở mùa này và mùa trước.</summary>
    private static async Task<LeagueBaseline> BaselineAsync(AppDbContext db, int leagueId, int season, CancellationToken ct)
    {
        var finished = db.Fixtures.AsNoTracking().Where(f => f.LeagueId == leagueId && f.Season >= season - 1 && f.Season <= season
            && f.HomeGoals != null && f.AwayGoals != null && (f.Status == "FT" || f.Status == "AET" || f.Status == "PEN"));
        var goals = await finished.GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Avg = g.Average(f => (double)(f.HomeGoals!.Value + f.AwayGoals!.Value)) })
            .FirstOrDefaultAsync(ct);
        var stats = await db.FixtureStats.AsNoTracking()
            .Where(s => s.HomeCorners != null && s.AwayCorners != null && finished.Any(f => f.Id == s.FixtureId))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Corners = g.Average(s => (double)(s.HomeCorners!.Value + s.AwayCorners!.Value)),
                Cards = g.Average(s => (double)((s.HomeYellow ?? 0) + (s.AwayYellow ?? 0) + (s.HomeRed ?? 0) + (s.AwayRed ?? 0))),
            })
            .FirstOrDefaultAsync(ct);

        return new LeagueBaseline(goals?.Count ?? 0, goals?.Avg, stats?.Count ?? 0, stats?.Corners, stats?.Cards);
    }

    // ------------------------------------------------------------------ Lưu & chấm khuyến nghị

    /// <summary>
    /// Cập nhật khuyến nghị của các trận sắp đá (bản cuối trước giờ bóng lăn là bản chốt) và chấm các khuyến nghị đã có kết quả.
    /// </summary>
    public async Task<string?> RefreshPicksAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = UtcNow;
        var until = now + Lookahead;
        var fixtures = await db.Fixtures.AsNoTracking()
            .Where(f => f.KickoffUtc > now && f.KickoffUtc <= until && (f.Status == "NS" || f.Status == "TBD") && f.League!.IsTracked)
            .ToListAsync(ct);
        var analyses = await AnalyzeAsync(db, fixtures, ct);
        var ids = fixtures.Select(f => f.Id).ToList();
        var stored = (await db.AnalysisPicks.Where(p => ids.Contains(p.FixtureId)).ToListAsync(ct))
            .ToDictionary(p => (p.FixtureId, p.Market));

        var upserted = 0;
        foreach (var (fixtureId, a) in analyses)
        {
            var markets = new HashSet<Market>();
            foreach (var pick in a.Analysis.Picks)
            {
                markets.Add(pick.Market);
                if (!stored.TryGetValue((fixtureId, pick.Market), out var row))
                {
                    row = new AnalysisPick { FixtureId = fixtureId, Market = pick.Market, CreatedAtUtc = now };
                    db.AnalysisPicks.Add(row);
                }

                row.Side = pick.Side;
                row.Line = pick.Line;
                row.Odds = pick.Odds;
                row.Score = (decimal)pick.Score;
                row.Group = pick.Group;
                row.WinProbability = Math.Round((decimal)pick.Probability.WinAny, 4);
                row.ExpectedValue = Math.Round((decimal)pick.ExpectedValue, 4);
                row.Label = pick.Label.Length > 200 ? pick.Label[..200] : pick.Label;
                row.Flags = pick.Flags.Count == 0 ? null : string.Join(",", pick.Flags.Distinct());
                row.Sample = pick.Sample;
                row.UpdatedAtUtc = now;
                row.Outcome = BetOutcome.Pending;
                upserted++;
            }

            foreach (var gone in stored.Values.Where(p => p.FixtureId == fixtureId && !markets.Contains(p.Market)))
            {
                db.AnalysisPicks.Remove(gone);
            }
        }

        await db.SaveChangesAsync(ct);
        var patterns = await SavePatternsAsync(db, analyses, now, ct);
        var settled = await SettleAsync(db, now, ct);
        var settledPatterns = await SettlePatternsAsync(db, now, ct);
        return upserted == 0 && settled == 0 && patterns == 0 && settledPatterns == 0
            ? null
            : $"Phân tích chuyên sâu: {analyses.Count} trận, {upserted} khuyến nghị; chấm {settled} khuyến nghị. Tài 3 / chấp 0.75–1: {patterns} trận, chấm {settledPatterns}.";
    }

    // ------------------------------------------------------------------ Bộ chỉ số Tài 3.0 / chấp 0.75–1

    /// <summary>Trận sắp đá khớp mẫu Tài 3.0 / chấp 0.75 / chấp 1.0 cửa trên, chấm theo số liệu mới nhất.</summary>
    public async Task<List<(MatchAnalysisResult Match, LinePatternResult Pattern)>> LinePatternsAsync(TimeSpan window, CancellationToken ct = default)
    {
        var upcoming = await UpcomingAsync(window, null, ct);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var lineups = await LineupsKnownAsync(db, upcoming.Select(r => r.Fixture.Id).ToList(), ct);
        var now = UtcNow;
        return upcoming
            .SelectMany(r => LinePatternScorer.Score(r.Input, r.Analysis, now, lineups.Contains(r.Fixture.Id)).Select(p => (r, p)))
            .ToList();
    }

    public sealed record LinePatternStat(LinePattern? Pattern, AnalysisGroup? Group, int Count, int Win, int HalfWin, int Push, int HalfLoss, int Loss, decimal Profit)
    {
        public decimal? WinRate => Win + HalfWin / 2m + Loss + HalfLoss / 2m is var n && n > 0 ? (Win + HalfWin / 2m) / n : null;
    }

    /// <summary>Thành tích bộ chỉ số theo mẫu × nhóm điểm (≥ 8 / 6–7.9 / &lt; 6), tính trên các trận đã chấm.</summary>
    public async Task<(List<LinePatternStat> Stats, List<LinePatternPick> Recent)> LinePatternHistoryAsync(int recent = 30, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var settled = await db.LinePatternPicks.AsNoTracking()
            .Where(p => p.Outcome != BetOutcome.Pending && p.Outcome != BetOutcome.Void)
            .Select(p => new { p.Pattern, p.Group, p.Outcome, p.Profit })
            .ToListAsync(ct);
        LinePatternStat Stat(LinePattern? pattern, AnalysisGroup? group, IEnumerable<BetOutcome> outcomes, decimal profit)
        {
            var o = outcomes.ToList();
            return new LinePatternStat(pattern, group, o.Count, o.Count(x => x == BetOutcome.Win), o.Count(x => x == BetOutcome.HalfWin),
                o.Count(x => x == BetOutcome.Push), o.Count(x => x == BetOutcome.HalfLoss), o.Count(x => x == BetOutcome.Loss), profit);
        }

        var stats = new List<LinePatternStat>();
        foreach (var pattern in Enum.GetValues<LinePattern>())
        {
            var rows = settled.Where(x => x.Pattern == pattern).ToList();
            stats.Add(Stat(pattern, null, rows.Select(x => x.Outcome), rows.Sum(x => x.Profit ?? 0)));
            foreach (var group in new[] { AnalysisGroup.A, AnalysisGroup.B, AnalysisGroup.C })
            {
                var g = rows.Where(x => x.Group == group).ToList();
                stats.Add(Stat(pattern, group, g.Select(x => x.Outcome), g.Sum(x => x.Profit ?? 0)));
            }
        }

        var recentRows = await db.LinePatternPicks.AsNoTracking().Include(p => p.Fixture)
            .Where(p => p.Outcome != BetOutcome.Pending)
            .OrderByDescending(p => p.Fixture!.KickoffUtc)
            .Take(recent)
            .ToListAsync(ct);
        return (stats, recentRows);
    }

    private static async Task<HashSet<int>> LineupsKnownAsync(AppDbContext db, List<int> fixtureIds, CancellationToken ct)
    {
        var known = new HashSet<int>();
        foreach (var chunk in fixtureIds.Chunk(Chunk))
        {
            known.UnionWith(await db.FixtureLineups.AsNoTracking().Where(l => chunk.Contains(l.FixtureId)).Select(l => l.FixtureId).Distinct().ToListAsync(ct));
        }

        return known;
    }

    /// <summary>Lưu trận khớp mẫu (bản cuối trước giờ bóng lăn là bản chốt); kèo đã dịch khỏi mẫu thì bỏ bản còn chờ.</summary>
    private static async Task<int> SavePatternsAsync(AppDbContext db, Dictionary<int, MatchAnalysisResult> analyses, DateTime now, CancellationToken ct)
    {
        if (analyses.Count == 0)
        {
            return 0;
        }

        var ids = analyses.Keys.ToList();
        var lineups = await LineupsKnownAsync(db, ids, ct);
        var stored = new Dictionary<(int, LinePattern), LinePatternPick>();
        foreach (var chunk in ids.Chunk(Chunk))
        {
            foreach (var row in await db.LinePatternPicks.Where(p => chunk.Contains(p.FixtureId)).ToListAsync(ct))
            {
                stored[(row.FixtureId, row.Pattern)] = row;
            }
        }

        var matched = 0;
        foreach (var (fixtureId, a) in analyses)
        {
            var results = LinePatternScorer.Score(a.Input, a.Analysis, now, lineups.Contains(fixtureId));
            foreach (var r in results)
            {
                if (!stored.TryGetValue((fixtureId, r.Pattern), out var row))
                {
                    row = new LinePatternPick { FixtureId = fixtureId, Pattern = r.Pattern, CreatedAtUtc = now };
                    db.LinePatternPicks.Add(row);
                }

                row.Side = r.Side;
                row.Line = r.Line;
                row.Odds = r.Odds;
                row.Score = (decimal)r.Score;
                row.Group = r.Group;
                row.WinProbability = Math.Round((decimal)r.Probability.WinAny, 4);
                row.PushProbability = Math.Round((decimal)r.Probability.Push, 4);
                row.ExpectedValue = Math.Round((decimal)r.ExpectedValue, 4);
                row.MinOdds = r.MinOdds;
                row.Label = Cut(r.Label, 200);
                row.EntryText = Cut(r.Entry.Text, 400);
                row.Indicators = Cut(string.Join("; ", r.Indicators.Where(i => i.Points != 0).Select(i => $"{i.Name} {(i.Points > 0 ? "+" : "−")}{Math.Abs(i.Points):0.##}")), 1000);
                row.UpdatedAtUtc = now;
                row.Outcome = BetOutcome.Pending;
                matched++;
            }

            var current = results.Select(r => r.Pattern).ToHashSet();
            foreach (var gone in stored.Values.Where(p => p.FixtureId == fixtureId && p.Outcome == BetOutcome.Pending && !current.Contains(p.Pattern)))
            {
                db.LinePatternPicks.Remove(gone);
            }
        }

        await db.SaveChangesAsync(ct);
        return matched;
    }

    private static async Task<int> SettlePatternsAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        var pending = await db.LinePatternPicks
            .Include(p => p.Fixture)
            .Where(p => p.Outcome == BetOutcome.Pending && p.Fixture!.KickoffUtc <= now)
            .ToListAsync(ct);
        var settled = 0;
        foreach (var pick in pending)
        {
            var f = pick.Fixture!;
            if (f.IsCancelled || f.Status == "PST" && f.KickoffUtc < now.AddDays(-2))
            {
                (pick.Outcome, pick.Profit) = (BetOutcome.Void, 0);
            }
            else if (f.IsFinished && f.HomeGoals is { } h && f.AwayGoals is { } a)
            {
                var r = Settlement.Settle(pick.Side, pick.Line, pick.Odds, h, a);
                (pick.Outcome, pick.Profit) = (r.Outcome, r.Profit);
            }
            else
            {
                continue;
            }

            pick.SettledAtUtc = now;
            settled++;
        }

        await db.SaveChangesAsync(ct);
        return settled;
    }

    private static string Cut(string text, int max) => text.Length > max ? text[..max] : text;

    private static async Task<int> SettleAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        var pending = await db.AnalysisPicks
            .Include(p => p.Fixture)
            .Where(p => p.Outcome == BetOutcome.Pending && p.Fixture!.KickoffUtc <= now)
            .ToListAsync(ct);
        if (pending.Count == 0)
        {
            return 0;
        }

        var ids = pending.Select(p => p.FixtureId).Distinct().ToList();
        var stats = await db.FixtureStats.AsNoTracking().Where(s => ids.Contains(s.FixtureId)).ToDictionaryAsync(s => s.FixtureId, ct);
        var settled = 0;
        foreach (var pick in pending)
        {
            var f = pick.Fixture!;
            BetOutcome? outcome = null;
            decimal profit = 0;
            if (f.IsCancelled || f.Status == "PST" && f.KickoffUtc < now.AddDays(-2))
            {
                outcome = BetOutcome.Void;
            }
            else if (f.IsFinished)
            {
                var (home, away) = pick.Market switch
                {
                    Market.CornersOverUnder or Market.CornersHandicap => (stats.GetValueOrDefault(f.Id)?.HomeCorners, stats.GetValueOrDefault(f.Id)?.AwayCorners),
                    Market.CardsOverUnder => (Cards(stats.GetValueOrDefault(f.Id), true), Cards(stats.GetValueOrDefault(f.Id), false)),
                    _ => (f.HomeGoals, f.AwayGoals),
                };

                if (home is { } h && away is { } a)
                {
                    var r = Settlement.Settle(pick.Side, pick.Line, pick.Odds, h, a);
                    (outcome, profit) = (r.Outcome, r.Profit);
                }
                else if (stats.GetValueOrDefault(f.Id) is { IsFinal: true } || f.KickoffUtc < now.AddDays(-3))
                {
                    outcome = BetOutcome.Void; // không có số liệu góc/thẻ để chấm
                }
            }

            if (outcome is { } o)
            {
                pick.Outcome = o;
                pick.Profit = profit;
                pick.SettledAtUtc = now;
                settled++;
            }
        }

        await db.SaveChangesAsync(ct);
        return settled;
    }

    private static int? Cards(FixtureStats? s, bool home) => s is null
        ? null
        : home ? s.HomeYellow + (s.HomeRed ?? 0) : s.AwayYellow + (s.AwayRed ?? 0);

    /// <summary>Cảnh báo In-play cho các trận đang đá (thống kê trực tiếp + DNA + kèo đóng + HLV/tân binh mới).</summary>
    public static async Task<Dictionary<int, IReadOnlyList<InPlayAlert>>> InPlayAlertsAsync(
        AppDbContext db, IReadOnlyCollection<Fixture> fixtures, ContextSet contexts, CancellationToken ct)
    {
        var result = new Dictionary<int, IReadOnlyList<InPlayAlert>>();
        var live = fixtures.Where(f => f.IsLive).ToList();
        if (live.Count == 0)
        {
            return result;
        }

        var ids = live.Select(f => f.Id).ToList();
        var leagueIds = live.Select(f => f.LeagueId).Distinct().ToList();
        var teamIds = live.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().ToList();
        var stats = await db.FixtureStats.AsNoTracking().Where(s => ids.Contains(s.FixtureId)).ToDictionaryAsync(s => s.FixtureId, ct);
        var leagueDna = await db.LeagueDnas.AsNoTracking().Where(d => leagueIds.Contains(d.LeagueId)).ToDictionaryAsync(d => d.LeagueId, d => d.Tags, ct);
        var teamDna = await db.TeamDnas.AsNoTracking().Where(d => teamIds.Contains(d.TeamId)).ToDictionaryAsync(d => d.TeamId, d => d.Tags, ct);
        var changes = (await db.TeamChanges.AsNoTracking().Where(c => teamIds.Contains(c.TeamId) && !c.IsIgnored).ToListAsync(ct)).ToLookup(c => c.TeamId);

        var history = new Dictionary<int, List<TeamMatch>>();
        if (changes.Count > 0)
        {
            var since = changes.SelectMany(g => g).Min(c => c.StartDate);
            var changed = changes.Select(g => g.Key).ToList();
            var rows = await db.Fixtures.AsNoTracking()
                .Where(f => (changed.Contains(f.HomeTeamId) || changed.Contains(f.AwayTeamId)) && f.KickoffUtc >= since
                    && f.HomeGoals != null && (f.Status == "FT" || f.Status == "AET" || f.Status == "PEN"))
                .Select(f => new { f.Id, f.KickoffUtc, f.HomeTeamId, f.AwayTeamId })
                .ToListAsync(ct);
            foreach (var teamId in changed)
            {
                history[teamId] = rows.Where(r => r.HomeTeamId == teamId || r.AwayTeamId == teamId)
                    .OrderByDescending(r => r.KickoffUtc)
                    .Select(r => new TeamMatch(r.Id, r.KickoffUtc, r.HomeTeamId == teamId, 0, 0, 0))
                    .ToList();
            }
        }

        foreach (var f in live)
        {
            var ctx = contexts.Contexts.GetValueOrDefault(f.Id);
            RegimeStatus? Regime(int teamId) => changes[teamId].Any()
                ? TeamFormBuilder.Regime(changes[teamId], history.GetValueOrDefault(teamId) ?? [], f.KickoffUtc)
                : null;

            var alerts = InPlayEngine.Evaluate(new InPlayInput
            {
                Fixture = f,
                Stats = stats.GetValueOrDefault(f.Id),
                HomeName = contexts.TeamName(f.HomeTeamId),
                AwayName = contexts.TeamName(f.AwayTeamId),
                LeagueTags = leagueDna.GetValueOrDefault(f.LeagueId),
                HomeTags = teamDna.GetValueOrDefault(f.HomeTeamId),
                AwayTags = teamDna.GetValueOrDefault(f.AwayTeamId),
                PreMatchHandicap = ctx?.Handicap?.Current.Line,
                PreMatchTotal = ctx?.Totals?.Current.Line,
                HomeRegime = Regime(f.HomeTeamId),
                AwayRegime = Regime(f.AwayTeamId),
            });
            if (alerts.Count > 0)
            {
                result[f.Id] = alerts;
            }
        }

        return result;
    }

    /// <summary>Khuyến nghị đã lưu (tốt nhất mỗi trận) để gắn nhãn A/B/C lên thẻ trận.</summary>
    public static async Task<Dictionary<int, AnalysisPick>> BestPicksAsync(AppDbContext db, IReadOnlyCollection<int> fixtureIds, CancellationToken ct)
    {
        var picks = new List<AnalysisPick>();
        foreach (var chunk in fixtureIds.Chunk(Chunk))
        {
            picks.AddRange(await db.AnalysisPicks.AsNoTracking().Where(p => chunk.Contains(p.FixtureId)).ToListAsync(ct));
        }

        return picks.GroupBy(p => p.FixtureId)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Group).ThenByDescending(p => p.Score).First());
    }

    private sealed class HistoryRow(int id, DateTime kickoffUtc, int homeTeamId, int awayTeamId, int homeGoals, int awayGoals)
    {
        public int Id { get; } = id;
        public DateTime KickoffUtc { get; } = kickoffUtc;
        public int HomeTeamId { get; } = homeTeamId;
        public int AwayTeamId { get; } = awayTeamId;
        public int HomeGoals { get; } = homeGoals;
        public int AwayGoals { get; } = awayGoals;
        public FixtureStats? Stats { get; set; }
        public decimal? ClosingHomeLine { get; set; }

        public TeamMatch For(int teamId)
        {
            var home = teamId == HomeTeamId;
            var s = Stats;
            return new TeamMatch(
                Id,
                KickoffUtc,
                home,
                home ? AwayTeamId : HomeTeamId,
                home ? HomeGoals : AwayGoals,
                home ? AwayGoals : HomeGoals,
                home ? s?.HomeCorners : s?.AwayCorners,
                home ? s?.AwayCorners : s?.HomeCorners,
                Cards(s, home),
                Cards(s, !home),
                home ? s?.HomeXg : s?.AwayXg,
                home ? s?.AwayXg : s?.HomeXg,
                home ? s?.HomeShots : s?.AwayShots,
                home ? s?.HomeShotsOnTarget : s?.AwayShotsOnTarget,
                ClosingHomeLine is { } l ? (home ? l : -l) : null);
        }
    }
}
