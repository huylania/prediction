using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prediction.Core.Entities;
using Prediction.Web.ApiFootball;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

public sealed class SyncOptions
{
    public const string Section = "Sync";

    public bool Enabled { get; set; } = true;
    public int LeaguesIntervalMinutes { get; set; } = 1440;
    public int FixturesIntervalMinutes { get; set; } = 120;
    public int OddsIntervalMinutes { get; set; } = 60;
    public int ClosingOddsIntervalMinutes { get; set; } = 15;
    public int ClosingWindowMinutes { get; set; } = 180;
    public int AdviceIntervalMinutes { get; set; } = 360;
    public int AdviceLookaheadDays { get; set; } = 3;
    public int SuggestionsIntervalMinutes { get; set; } = 5;
    public int LiveIntervalMinutes { get; set; } = 1;
}

/// <summary>Các tác vụ lấy dữ liệu từ API-Football và ghi vào CSDL.</summary>
public sealed class DataSyncService(
    AppDbContext db,
    ApiFootballClient api,
    IOptions<SyncOptions> options,
    TimeProvider clock,
    ILogger<DataSyncService> logger)
{
    public int RequestCount => api.RequestCount;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Cập nhật tên, loại giải và mùa hiện tại cho các giải đang theo dõi.</summary>
    public async Task<string> RefreshLeaguesAsync(CancellationToken ct)
    {
        var leagues = await db.Leagues.Where(l => l.IsTracked && !l.IsDemo).ToListAsync(ct);
        var missing = new List<string>();
        foreach (var league in leagues)
        {
            var item = await api.GetLeagueAsync(league.Id, ct);
            if (item is null)
            {
                missing.Add($"{league.Name} (id {league.Id})");
                continue;
            }

            Apply(league, item);
        }

        await db.SaveChangesAsync(ct);
        return missing.Count == 0
            ? $"Đã cập nhật {leagues.Count} giải."
            : $"Đã cập nhật {leagues.Count - missing.Count} giải. Không tìm thấy trên API: {string.Join(", ", missing)} — kiểm tra lại id trong Cài đặt.";
    }

    public static void Apply(League league, LeagueItem item)
    {
        league.Name = item.League.Name;
        league.Type = item.League.Type;
        league.LogoUrl = item.League.Logo;
        league.Country = item.Country?.Name ?? league.Country;
        var current = item.Seasons.FirstOrDefault(s => s.Current) ?? item.Seasons.OrderByDescending(s => s.Year).FirstOrDefault();
        if (current is not null)
        {
            league.CurrentSeason = current.Year;
            if (DateOnly.TryParse(current.Start, System.Globalization.CultureInfo.InvariantCulture, out var start))
            {
                league.SeasonYearIsEndYear = start.Year < current.Year;
            }
        }
    }

    public Task<List<LeagueItem>> SearchLeaguesAsync(string query, CancellationToken ct) => api.SearchLeaguesAsync(query, ct);

    /// <summary>Thêm (hoặc bật lại) một giải để theo dõi.</summary>
    public async Task<string> AddLeagueAsync(int leagueId, CancellationToken ct)
    {
        var item = await api.GetLeagueAsync(leagueId, ct) ?? throw new ApiFootballException($"Không tìm thấy giải id {leagueId}.");
        var league = await db.Leagues.FindAsync([leagueId], ct);
        if (league is null)
        {
            league = new League { Id = leagueId };
            db.Leagues.Add(league);
        }

        Apply(league, item);
        league.IsTracked = true;
        await db.SaveChangesAsync(ct);
        return $"Đã thêm {league.Name} (mùa {league.CurrentSeason}).";
    }

    /// <summary>Lịch + kết quả mùa hiện tại của các giải đang theo dõi.</summary>
    public async Task<string> SyncCurrentFixturesAsync(CancellationToken ct)
    {
        var leagues = await db.Leagues.AsNoTracking().Where(l => l.IsTracked && !l.IsDemo).ToListAsync(ct);
        var total = 0;
        foreach (var league in leagues)
        {
            total += await SyncFixturesAsync(league.Id, league.CurrentSeason, ct);
        }

        return $"Đã cập nhật {total} trận của {leagues.Count} giải.";
    }

    /// <summary>Nạp toàn bộ trận của một giải-mùa (dùng cả cho "Tải mùa cũ").</summary>
    public async Task<int> SyncFixturesAsync(int leagueId, int season, CancellationToken ct)
    {
        var league = await db.Leagues.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leagueId, ct);
        var items = await api.GetFixturesAsync(leagueId, season, ct);
        if (items.Count == 0)
        {
            return 0;
        }

        var teamIds = items.SelectMany(i => new[] { i.Teams.Home.Id, i.Teams.Away.Id }).Distinct().ToList();
        var teams = await db.Teams.Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        foreach (var teamRef in items.SelectMany(i => new[] { i.Teams.Home, i.Teams.Away }).DistinctBy(t => t.Id))
        {
            if (!teams.TryGetValue(teamRef.Id, out var team))
            {
                team = new Team { Id = teamRef.Id };
                db.Teams.Add(team);
                teams[team.Id] = team;
            }

            team.Name = teamRef.Name;
            team.LogoUrl = teamRef.Logo;
        }

        var ids = items.Select(i => i.Fixture.Id).Distinct().ToList();
        var fixtures = new Dictionary<int, Fixture>();
        foreach (var chunk in ids.Chunk(500))
        {
            foreach (var f in await db.Fixtures.Where(f => chunk.Contains(f.Id)).ToListAsync(ct))
            {
                fixtures[f.Id] = f;
            }
        }

        var now = Now;
        foreach (var item in items)
        {
            if (fixtures.TryGetValue(item.Fixture.Id, out var fixture))
            {
                ApiMapper.Apply(fixture, item, league?.Type, now);
                continue;
            }

            // Gán Id trước khi Add: Id là khóa (lấy từ API), không được đổi sau khi đã theo dõi.
            fixture = new Fixture();
            ApiMapper.Apply(fixture, item, league?.Type, now);
            db.Fixtures.Add(fixture);
            fixtures[fixture.Id] = fixture;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đồng bộ {Count} trận giải {League} mùa {Season}", items.Count, leagueId, season);
        return items.Count;
    }

    /// <summary>
    /// Tỉ số và phút thi đấu của các trận đang diễn ra. Chỉ gọi API khi có trận trong khung giờ thi đấu.
    /// Trận rời khỏi danh sách "live" được lấy lại theo id để có trạng thái cuối (FT, hoãn...).
    /// Trả về thông điệp chỉ khi có trận vừa kết thúc, để nhật ký không bị lặp mỗi phút.
    /// </summary>
    public async Task<string?> SyncLiveAsync(CancellationToken ct)
    {
        var now = Now;
        var leagues = await db.Leagues.AsNoTracking().Where(l => l.IsTracked && !l.IsDemo).ToDictionaryAsync(l => l.Id, ct);
        var leagueIds = leagues.Keys.ToList();
        var windowStart = now.AddHours(-4);
        var candidates = (await db.Fixtures
                .Where(f => leagueIds.Contains(f.LeagueId) && f.KickoffUtc <= now && f.KickoffUtc >= windowStart)
                .ToListAsync(ct))
            .Where(f => !f.IsFinished && !f.IsCancelled && f.Status != "PST")
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var live = await api.GetLiveFixturesAsync(leagueIds, ct);
        var liveIds = live.Select(i => i.Fixture.Id).ToHashSet();
        var others = candidates.Where(f => !liveIds.Contains(f.Id)).Select(f => f.Id).ToList();
        var items = live.ToList();
        foreach (var chunk in others.Chunk(20))
        {
            items.AddRange(await api.GetFixturesByIdsAsync(chunk, ct));
        }

        var byId = candidates.ToDictionary(f => f.Id);
        var finished = 0;
        foreach (var item in items)
        {
            if (!byId.TryGetValue(item.Fixture.Id, out var fixture))
            {
                fixture = await db.Fixtures.FindAsync([item.Fixture.Id], ct);
                if (fixture is null)
                {
                    continue; // trận chưa có trong CSDL: lần đồng bộ lịch tiếp theo sẽ thêm
                }
            }

            var wasFinished = fixture.IsFinished;
            ApiMapper.Apply(fixture, item, leagues.GetValueOrDefault(fixture.LeagueId)?.Type, now);
            if (!wasFinished && fixture.IsFinished)
            {
                finished++;
            }
        }

        await db.SaveChangesAsync(ct);
        return finished == 0 ? null : $"Trực tiếp: {live.Count} trận đang đá, {finished} trận vừa kết thúc.";
    }

    /// <summary>Kèo theo giải cho mọi trận sắp đá đã có kèo (bắt kèo mở và biến động trong ngày).</summary>
    public async Task<string> SyncLeagueOddsAsync(CancellationToken ct)
    {
        var leagues = await db.Leagues.AsNoTracking().Where(l => l.IsTracked && !l.IsDemo).ToListAsync(ct);
        var stored = 0;
        foreach (var league in leagues)
        {
            var items = await api.GetLeagueOddsAsync(league.Id, league.CurrentSeason, ct);
            stored += await StoreOddsAsync(items, ct);
        }

        return $"Kèo theo giải: {stored} thay đổi mới.";
    }

    /// <summary>Kèo từng trận trong cửa sổ trước giờ bóng lăn (bắt biến động phút chót và kèo đóng).</summary>
    public async Task<string?> SyncClosingOddsAsync(CancellationToken ct)
    {
        var now = Now;
        var until = now.AddMinutes(options.Value.ClosingWindowMinutes);
        var ids = await TrackedUpcomingAsync(now, until, ct);
        if (ids.Count == 0)
        {
            return null;
        }

        var stored = 0;
        foreach (var id in ids)
        {
            stored += await StoreOddsAsync(await api.GetFixtureOddsAsync(id, ct), ct);
        }

        return $"Kèo sát giờ: {ids.Count} trận, {stored} thay đổi mới.";
    }

    /// <summary>Tư vấn của API cho các trận sắp tới. <paramref name="finalOnly"/>: chỉ trận sắp bóng lăn (~1 giờ nữa).</summary>
    public async Task<string?> SyncAdviceAsync(bool finalOnly, CancellationToken ct)
    {
        var now = Now;
        var (from, until) = finalOnly
            ? (now.AddMinutes(30), now.AddMinutes(90))
            : (now, now.AddDays(options.Value.AdviceLookaheadDays));
        var ids = await TrackedUpcomingAsync(from, until, ct);
        if (finalOnly && ids.Count > 0)
        {
            // Chỉ lấy lại nếu bản đang có được lấy sớm hơn 2 giờ trước trận.
            var fresh = await db.ApiAdvices.Where(a => ids.Contains(a.FixtureId) && a.FetchedAtUtc >= a.Fixture!.KickoffUtc.AddHours(-2))
                .Select(a => a.FixtureId).ToListAsync(ct);
            ids = ids.Except(fresh).ToList();
        }

        if (ids.Count == 0)
        {
            return null;
        }

        var updated = 0;
        foreach (var id in ids)
        {
            var item = await api.GetPredictionAsync(id, ct);
            if (item is null)
            {
                continue;
            }

            var advice = await db.ApiAdvices.FirstOrDefaultAsync(a => a.FixtureId == id, ct);
            if (advice is null)
            {
                advice = new ApiAdvice { FixtureId = id };
                db.ApiAdvices.Add(advice);
            }

            ApiMapper.Apply(advice, item, Now);
            updated++;
        }

        await db.SaveChangesAsync(ct);
        return $"Tư vấn API: {updated} trận.";
    }

    private async Task<List<int>> TrackedUpcomingAsync(DateTime from, DateTime until, CancellationToken ct) =>
        await db.Fixtures.AsNoTracking()
            .Where(f => f.KickoffUtc > from && f.KickoffUtc <= until && (f.Status == "NS" || f.Status == "TBD") && f.League!.IsTracked && !f.League.IsDemo)
            .OrderBy(f => f.KickoffUtc)
            .Select(f => f.Id)
            .ToListAsync(ct);

    /// <summary>Ghi kèo cho các trận chưa bóng lăn; chỉ ghi khi khác bản gần nhất.</summary>
    private async Task<int> StoreOddsAsync(IReadOnlyCollection<OddsItem> items, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        var now = Now;
        var ids = items.Select(i => i.Fixture.Id).Distinct().ToList();
        var kickoffs = await db.Fixtures.AsNoTracking().Where(f => ids.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.KickoffUtc, ct);
        var latest = (await db.OddsSnapshots.AsNoTracking()
                .Where(s => ids.Contains(s.FixtureId))
                .GroupBy(s => new { s.FixtureId, s.Market })
                .Select(g => g.OrderByDescending(s => s.FetchedAtUtc).ThenByDescending(s => s.Id).First())
                .ToListAsync(ct))
            .ToDictionary(s => (s.FixtureId, s.Market));

        var stored = 0;
        foreach (var item in items)
        {
            if (!kickoffs.TryGetValue(item.Fixture.Id, out var kickoff) || kickoff <= now)
            {
                continue;
            }

            foreach (var quote in OddsParser.Parse(item, api.BookmakerId))
            {
                var snapshot = OddsStore.NextSnapshot(latest.GetValueOrDefault((item.Fixture.Id, quote.Market)), item.Fixture.Id, quote, api.BookmakerId, item.Update?.UtcDateTime, now, OddsSource.Api);
                if (snapshot is not null)
                {
                    db.OddsSnapshots.Add(snapshot);
                    latest[(item.Fixture.Id, quote.Market)] = snapshot;
                    stored++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return stored;
    }
}

public static class OddsStore
{
    /// <summary>Snapshot mới cần ghi, hoặc null nếu mức kèo và tiền kèo không đổi so với bản gần nhất.</summary>
    public static OddsSnapshot? NextSnapshot(OddsSnapshot? latest, int fixtureId, ParsedQuote quote, int bookmakerId, DateTime? bookmakerUpdatedUtc, DateTime nowUtc, OddsSource source)
    {
        if (latest is not null && latest.SameQuote(quote.Line, quote.PriceA, quote.PriceB))
        {
            return null;
        }

        return new OddsSnapshot
        {
            FixtureId = fixtureId,
            Market = quote.Market,
            Line = quote.Line,
            PriceA = quote.PriceA,
            PriceB = quote.PriceB,
            BookmakerId = bookmakerId,
            BookmakerUpdatedAtUtc = bookmakerUpdatedUtc,
            FetchedAtUtc = nowUtc,
            Source = source,
        };
    }
}
