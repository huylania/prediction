using Microsoft.EntityFrameworkCore;
using Prediction.Core.Engine;
using Prediction.Core.Entities;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>
/// Sinh/cập nhật gợi ý cho trận sắp đá, chốt theo kèo đóng lúc bóng lăn và chấm khi có kết quả.
/// </summary>
public sealed class SuggestionService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    /// <summary>Gợi ý được sinh cho các trận trong khoảng này.</summary>
    public static readonly TimeSpan Lookahead = TimeSpan.FromDays(7);

    /// <summary>Trả về mô tả thay đổi, hoặc null nếu không có gì thay đổi.</summary>
    public async Task<string?> RefreshAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var rules = await db.Rules.AsNoTracking().Where(r => r.IsActive).ToListAsync(ct);
        var allRules = await db.Rules.AsNoTracking().ToDictionaryAsync(r => r.Id, ct);

        var horizon = now + Lookahead;
        var upcoming = await db.Fixtures.AsNoTracking()
            .Where(f => f.KickoffUtc > now && f.KickoffUtc <= horizon && (f.Status == "NS" || f.Status == "TBD"))
            .ToListAsync(ct);

        // Gợi ý còn mở (chưa chốt hoặc chờ kết quả) + mọi gợi ý của các trận sắp đá.
        var tracked = new Dictionary<long, Suggestion>();
        foreach (var s in await db.Suggestions.Include(s => s.Fixture).Where(s => !s.IsFrozen || s.Outcome == BetOutcome.Pending).ToListAsync(ct))
        {
            tracked[s.Id] = s;
        }

        var upcomingIds = upcoming.Select(f => f.Id).ToHashSet();
        foreach (var chunk in upcomingIds.Chunk(500))
        {
            foreach (var s in await db.Suggestions.Include(s => s.Fixture).Where(s => chunk.Contains(s.FixtureId)).ToListAsync(ct))
            {
                tracked[s.Id] = s;
            }
        }

        var open = tracked.Values.ToList();

        // Trận đã chốt nhưng bị dời lịch về sau: mở lại.
        foreach (var s in open.Where(s => s.IsFrozen && upcomingIds.Contains(s.FixtureId)))
        {
            s.IsFrozen = false;
            s.Outcome = BetOutcome.Pending;
            s.Profit = null;
            s.SettledAtUtc = null;
            s.UpdatedAtUtc = now;
        }

        var fixtures = upcoming
            .Concat(open.Select(s => s.Fixture!))
            .DistinctBy(f => f.Id)
            .ToList();
        var contexts = await ContextLoader.LoadAsync(db, fixtures, now, ct);

        int created = 0, frozen = 0, settled = 0;

        // 1) Trận sắp đá: sinh mới / cập nhật theo kèo hiện tại.
        var byKey = open.Where(s => upcomingIds.Contains(s.FixtureId)).ToLookup(s => (s.RuleId, s.FixtureId));
        foreach (var fixture in upcoming)
        {
            var ctx = contexts.Contexts[fixture.Id];
            foreach (var rule in rules)
            {
                var pick = RuleEvaluator.Evaluate(rule, ctx);
                var existing = byKey[(rule.Id, fixture.Id)].ToList();
                foreach (var s in existing.Where(s => pick is null || s.Side != pick.Side))
                {
                    s.CurrentlyMatches = false;
                    s.UpdatedAtUtc = now;
                }

                if (pick is null)
                {
                    continue;
                }

                var same = existing.FirstOrDefault(s => s.Side == pick.Side);
                if (same is null)
                {
                    db.Suggestions.Add(New(rule.Id, fixture.Id, pick, now));
                    created++;
                }
                else
                {
                    Update(same, pick, now);
                    same.CurrentlyMatches = true;
                }
            }
        }

        // 2) Đã qua giờ bóng lăn: chốt theo kèo đóng.
        foreach (var s in open.Where(s => !s.IsFrozen && s.Fixture!.KickoffUtc <= now))
        {
            var fixture = s.Fixture!;
            var pick = allRules.TryGetValue(s.RuleId, out var rule) ? RuleEvaluator.Evaluate(rule, contexts.Contexts[fixture.Id]) : null;
            s.IsFrozen = true;
            s.UpdatedAtUtc = now;
            frozen++;
            if (pick is not null && pick.Side == s.Side)
            {
                Update(s, pick, now);
                s.CurrentlyMatches = true;
            }
            else
            {
                s.CurrentlyMatches = false;
                s.Outcome = BetOutcome.Void;
                s.Profit = 0;
                s.SettledAtUtc = now;
            }
        }

        // 3) Chấm các gợi ý đã chốt khi trận có kết quả.
        foreach (var s in open.Where(s => s.IsFrozen && s.Outcome == BetOutcome.Pending))
        {
            if (Settle(s, s.Fixture!, now))
            {
                settled++;
            }
        }

        await db.SaveChangesAsync(ct);
        return created + frozen + settled == 0 ? null : $"Gợi ý: {created} mới, {frozen} chốt kèo, {settled} đã chấm.";
    }

    /// <summary>
    /// Chỉ dùng cho dữ liệu demo: sinh gợi ý cho các trận ĐÃ ĐÁ như thể hệ thống đã chạy từ trước,
    /// để trang thành tích có số liệu.
    /// </summary>
    public async Task BackfillAsync(int leagueId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var rules = await db.Rules.AsNoTracking().Where(r => r.IsActive).ToListAsync(ct);
        var finished = (await db.Fixtures.AsNoTracking().Where(f => f.LeagueId == leagueId && f.KickoffUtc < now).ToListAsync(ct))
            .Where(f => f.IsFinished)
            .ToList();
        var contexts = await ContextLoader.LoadAsync(db, finished, now, ct);
        var existing = (await db.Suggestions.Where(s => s.Fixture!.LeagueId == leagueId).Select(s => new { s.RuleId, s.FixtureId }).ToListAsync(ct))
            .Select(x => (x.RuleId, x.FixtureId))
            .ToHashSet();

        foreach (var fixture in finished)
        {
            foreach (var rule in rules)
            {
                if (existing.Contains((rule.Id, fixture.Id)) || RuleEvaluator.Evaluate(rule, contexts.Contexts[fixture.Id]) is not { } pick)
                {
                    continue;
                }

                var s = New(rule.Id, fixture.Id, pick, fixture.KickoffUtc);
                s.IsFrozen = true;
                Settle(s, fixture, now);
                db.Suggestions.Add(s);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static bool Settle(Suggestion s, Fixture fixture, DateTime now)
    {
        if (fixture.IsFinished)
        {
            var result = Settlement.Settle(s.Side, s.Line, s.Odds, fixture.HomeGoals!.Value, fixture.AwayGoals!.Value);
            s.Outcome = result.Outcome;
            s.Profit = result.Profit;
            s.SettledAtUtc = now;
            return true;
        }

        if (fixture.IsCancelled)
        {
            s.Outcome = BetOutcome.Void;
            s.Profit = 0;
            s.SettledAtUtc = now;
            return true;
        }

        return false;
    }

    private static Suggestion New(int ruleId, int fixtureId, Pick pick, DateTime now) => new()
    {
        RuleId = ruleId,
        FixtureId = fixtureId,
        Side = pick.Side,
        FirstLine = pick.Line,
        FirstOdds = pick.Odds,
        FirstMatchedAtUtc = now,
        Line = pick.Line,
        Odds = pick.Odds,
        SubjectRank = pick.SubjectRank,
        OpponentRank = pick.OpponentRank,
        CurrentlyMatches = true,
        UpdatedAtUtc = now,
    };

    private static void Update(Suggestion s, Pick pick, DateTime now)
    {
        s.Line = pick.Line;
        s.Odds = pick.Odds;
        s.SubjectRank = pick.SubjectRank;
        s.OpponentRank = pick.OpponentRank;
        s.UpdatedAtUtc = now;
    }
}
