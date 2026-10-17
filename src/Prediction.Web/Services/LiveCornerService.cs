using Microsoft.EntityFrameworkCore;
using Prediction.Core.Engine;
using Prediction.Core.Engine.LiveCorners;
using Prediction.Core.Entities;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>Một trận đang đá với phân tích góc live của hiệp hiện tại.</summary>
public sealed class LiveCornerMatch
{
    public required Fixture Fixture { get; init; }
    public string? League { get; init; }
    public required string Home { get; init; }
    public required string Away { get; init; }
    public required LiveCornerInput Input { get; init; }
    public required LiveCornerResult Result { get; init; }
    public LiveCornerOdds? Odds { get; init; }
    public DateTime? StatsAtUtc { get; init; }
    public LiveCornerPick? Pick { get; init; }

    /// <summary>Đang trong cửa sổ vàng (25–30' / 75–80').</summary>
    public bool InWindow => Result.InGoldenWindow;

    /// <summary>Sắp vào cửa sổ (15–24' / 60–74').</summary>
    public bool Approaching => Input.Window == CornerWindow.FirstHalf ? Input.Minute is >= 15 and < 25 : Input.Minute is >= 60 and < 75;
}

/// <summary>Mục "Góc Live" (file kỹ năng phan-tich-goc): phân tích trận đang đá, chốt và chấm khuyến nghị.</summary>
public sealed class LiveCornerService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    /// <summary>Các trận đang đá (hiệp 1 từ phút 15, hiệp 2 từ phút 60) kèm phân tích.</summary>
    public async Task<List<LiveCornerMatch>> LiveAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await LiveAsync(db, UtcNow, ct);
    }

    private static async Task<List<LiveCornerMatch>> LiveAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        var fixtures = (await db.Fixtures.AsNoTracking().Include(f => f.League)
                .Where(f => f.League!.IsTracked && f.KickoffUtc <= now && f.KickoffUtc >= now.AddHours(-3) && (f.Status == "1H" || f.Status == "2H"))
                .ToListAsync(ct))
            .Where(f => f.Status == "1H" ? f.Elapsed >= 15 : f.Elapsed >= 60)
            .OrderByDescending(f => f.Elapsed)
            .ToList();
        if (fixtures.Count == 0)
        {
            return [];
        }

        var ids = fixtures.Select(f => f.Id).ToList();
        var contexts = await ContextLoader.LoadAsync(db, fixtures, now, ct);
        var stats = await db.FixtureStats.AsNoTracking().Where(s => ids.Contains(s.FixtureId)).ToDictionaryAsync(s => s.FixtureId, ct);
        var snapshots = (await db.LiveStatSnapshots.AsNoTracking().Where(s => ids.Contains(s.FixtureId)).ToListAsync(ct)).ToLookup(s => s.FixtureId);
        var odds = (await db.LiveCornerOdds.AsNoTracking().Where(o => ids.Contains(o.FixtureId)).ToListAsync(ct)).ToLookup(o => (o.FixtureId, o.Window));
        var picks = await db.LiveCornerPicks.AsNoTracking().Where(p => ids.Contains(p.FixtureId)).ToListAsync(ct);

        var result = new List<LiveCornerMatch>();
        foreach (var f in fixtures)
        {
            if (!stats.TryGetValue(f.Id, out var s) || s.HomeCorners is null)
            {
                continue;
            }

            var window = f.Status == "1H" ? CornerWindow.FirstHalf : CornerWindow.SecondHalf;
            var minute = f.Elapsed ?? 0;
            var history = odds[(f.Id, window)].OrderBy(o => o.FetchedAtUtc).ToList();
            var current = history.LastOrDefault();
            var recentSpan = window == CornerWindow.FirstHalf ? 10 : 15;
            var baseline = snapshots[f.Id].Where(x => x.Minute <= minute - recentSpan + 2).OrderByDescending(x => x.Minute).FirstOrDefault();
            var ctx = contexts.Contexts.GetValueOrDefault(f.Id);

            var input = new LiveCornerInput
            {
                Window = window,
                Minute = minute,
                HomeCorners = s.HomeCorners ?? 0,
                AwayCorners = s.AwayCorners ?? 0,
                FirstHalfCorners = window == CornerWindow.SecondHalf ? s.FirstHalfHomeCorners + s.FirstHalfAwayCorners : null,
                Shots = s.HomeShots + s.AwayShots,
                HomeShots = s.HomeShots,
                AwayShots = s.AwayShots,
                HomePossession = s.HomePossession,
                ShotsOnTarget = s.HomeShotsOnTarget + s.AwayShotsOnTarget,
                BlockedShots = s.HomeBlocked + s.AwayBlocked,
                HomeGoals = f.HomeGoals ?? 0,
                AwayGoals = f.AwayGoals ?? 0,
                HomeRed = s.HomeRed ?? 0,
                AwayRed = s.AwayRed ?? 0,
                Line = current?.Line,
                OverOdds = current?.OverOdds,
                UnderOdds = current?.UnderOdds,
                OpenLine = history.FirstOrDefault()?.Line,
                RecentMinutes = baseline is null ? null : minute - baseline.Minute,
                RecentShots = baseline is null ? null : (s.HomeShots ?? 0) + (s.AwayShots ?? 0) - baseline.Shots,
                RecentCorners = baseline is null ? null : (s.HomeCorners ?? 0) + (s.AwayCorners ?? 0) - baseline.Corners,
                RecentBlocked = baseline is null ? null : (s.HomeBlocked ?? 0) + (s.AwayBlocked ?? 0) - (baseline.HomeBlocked ?? 0) - (baseline.AwayBlocked ?? 0),
                Motivation = MotivationOf(f, ctx, contexts.TableSizes.GetValueOrDefault((f.LeagueId, f.Season))),
                Favourite = ctx?.Handicap?.Current.Line switch { < 0 => PickSide.Home, > 0 => PickSide.Away, _ => null },
                PreMatchHandicap = ctx?.Handicap?.Current.Line is { } hl && hl != 0 ? Math.Abs(hl) : null,
            };

            result.Add(new LiveCornerMatch
            {
                Fixture = f,
                League = f.League?.Name,
                Home = contexts.TeamName(f.HomeTeamId),
                Away = contexts.TeamName(f.AwayTeamId),
                Input = input,
                Result = LiveCornerAnalyzer.Analyze(input),
                Odds = current,
                StatsAtUtc = s.UpdatedAtUtc,
                Pick = picks.FirstOrDefault(p => p.FixtureId == f.Id && p.Window == window),
            });
        }

        return result;
    }

    /// <summary>Động lực (K_moti) suy từ vòng đấu và BXH: knock-out / cuối mùa đua vô địch–trụ hạng → cao; cuối mùa giữa BXH → thấp.</summary>
    public static Motivation MotivationOf(Fixture f, Prediction.Core.Engine.MatchContext? ctx, int tableSize)
    {
        var round = f.Round ?? "";
        string[] knockout = ["Final", "Semi", "Quarter", "Round of", "Knockout", "Play-off", "Leg", "8th Finals", "16th Finals"];
        if (knockout.Any(k => round.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return Motivation.High;
        }

        if (ctx?.Home is { Rank: { } hr, Played: var played } && ctx.Away is { Rank: { } ar } && tableSize >= 8)
        {
            var rounds = 2 * (tableSize - 1);
            var late = played >= rounds * 0.8;
            bool Hot(int rank) => rank <= 2 || rank >= tableSize - 2;
            if (late && (Hot(hr) || Hot(ar)))
            {
                return Motivation.High;
            }

            if (played >= rounds * 0.9 && hr is > 5 && ar is > 5 && hr < tableSize - 4 && ar < tableSize - 4)
            {
                return Motivation.Low;
            }
        }

        return Motivation.Normal;
    }

    /// <summary>Chốt khuyến nghị trong cửa sổ vàng và chấm các khuyến nghị đã có kết quả.</summary>
    public async Task<string?> ProcessAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = UtcNow;
        var created = 0;
        foreach (var m in await LiveAsync(db, now, ct))
        {
            var r = m.Result;
            if (!r.InGoldenWindow || r.Side is not { } side || r.Line is not { } line || m.Pick is not null || m.Fixture.League?.IsDemo == true)
            {
                continue;
            }

            var odds = side == PickSide.Over ? m.Input.OverOdds : m.Input.UnderOdds;
            if (odds is null)
            {
                continue;
            }

            db.LiveCornerPicks.Add(new LiveCornerPick
            {
                FixtureId = m.Fixture.Id,
                Window = r.Window,
                Minute = r.Minute,
                Side = side,
                Line = line,
                Odds = odds.Value,
                CornersAtPick = r.Corners,
                ExpectedTotal = Math.Round((decimal)r.ExpectedTotal, 2),
                Edge = Math.Round((decimal)(r.Edge ?? 0), 2),
                WinProbability = Math.Round((decimal)(r.Probability?.WinAny ?? 0), 4),
                Confidence = (decimal)r.Confidence,
                CreatedAtUtc = now,
            });
            created++;
        }

        await db.SaveChangesAsync(ct);
        var settled = await SettleAsync(db, now, ct);
        return created == 0 && settled == 0 ? null : $"Góc Live: chốt {created} khuyến nghị, chấm {settled}.";
    }

    private static async Task<int> SettleAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        var pending = await db.LiveCornerPicks.Include(p => p.Fixture).Where(p => p.Outcome == BetOutcome.Pending).ToListAsync(ct);
        if (pending.Count == 0)
        {
            return 0;
        }

        var ids = pending.Select(p => p.FixtureId).ToList();
        var stats = await db.FixtureStats.AsNoTracking().Where(s => ids.Contains(s.FixtureId)).ToDictionaryAsync(s => s.FixtureId, ct);
        var settled = 0;
        foreach (var p in pending)
        {
            var f = p.Fixture!;
            var s = stats.GetValueOrDefault(f.Id);
            (int Home, int Away)? total = p.Window == CornerWindow.FirstHalf
                ? s?.FirstHalfHomeCorners is { } h1 && s.FirstHalfAwayCorners is { } a1 ? (h1, a1) : null
                : f.IsFinished && s?.HomeCorners is { } h && s.AwayCorners is { } a ? (h, a) : null;

            if (total is { } t)
            {
                var r = Settlement.Settle(p.Side, p.Line, p.Odds, t.Home, t.Away);
                p.Outcome = r.Outcome;
                p.Profit = r.Profit;
                p.FinalCorners = t.Home + t.Away;
            }
            else if (f.IsCancelled || f.KickoffUtc < now.AddHours(-6))
            {
                p.Outcome = BetOutcome.Void; // không có số góc để chấm
                p.Profit = 0;
            }
            else
            {
                continue;
            }

            p.SettledAtUtc = now;
            settled++;
        }

        await db.SaveChangesAsync(ct);
        return settled;
    }

    public async Task<List<LiveCornerPick>> PicksAsync(int take, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.LiveCornerPicks.AsNoTracking().Include(p => p.Fixture)
            .OrderByDescending(p => p.CreatedAtUtc).Take(take).ToListAsync(ct);
    }

    public async Task<Dictionary<int, string>> TeamNamesAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var list = ids.Distinct().ToList();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Teams.AsNoTracking().Where(t => list.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
    }
}
