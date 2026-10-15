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
    public int LiveIntervalMinutes { get; set; } = 2;

    /// <summary>Tải thống kê (góc, thẻ, sút, xG) các trận đã đá: chu kỳ và số request tối đa mỗi lần.</summary>
    public int StatsIntervalMinutes { get; set; } = 30;
    public int StatsMaxRequests { get; set; } = 20;

    /// <summary>Số mùa trước mùa hiện tại cần tải thống kê (1 = mùa hiện tại + mùa trước).</summary>
    public int StatsPreviousSeasons { get; set; } = 1;

    public int CoachesIntervalMinutes { get; set; } = 1440;
    public int InjuriesIntervalMinutes { get; set; } = 360;

    /// <summary>Cập nhật khuyến nghị phân tích chuyên sâu (không tốn request API).</summary>
    public int AnalysisIntervalMinutes { get; set; } = 10;

    /// <summary>Còn ít hơn số request này trong ngày thì tạm dừng các tác vụ phụ (thống kê, HLV, chấn thương, tư vấn).</summary>
    public int QuotaReserve { get; set; } = 1000;
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
                league.SeasonIsCalendarYear = DateOnly.TryParse(current.End, System.Globalization.CultureInfo.InvariantCulture, out var end)
                    && start.Year == end.Year;
            }

            if (current.Coverage is { } coverage)
            {
                league.HasStatsCoverage = coverage.Fixtures?.StatisticsFixtures ?? true;
                league.HasInjuriesCoverage = coverage.Injuries;
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
    /// Tỉ số, phút thi đấu và thống kê (góc, thẻ, sút, diễn biến bàn thắng) của các trận đang diễn ra.
    /// Chỉ gọi API khi có trận trong khung giờ thi đấu; lấy theo id (20 trận/request) để có kèm thống kê.
    /// Trả về thông điệp chỉ khi có trận vừa kết thúc, để nhật ký không bị lặp mỗi lần chạy.
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

        var ids = candidates.Select(f => f.Id).ToList();
        var stats = await db.FixtureStats.Where(s => ids.Contains(s.FixtureId)).ToDictionaryAsync(s => s.FixtureId, ct);
        var byId = candidates.ToDictionary(f => f.Id);
        var live = 0;
        var finished = 0;
        foreach (var chunk in ids.Chunk(20))
        {
            foreach (var item in await api.GetFixturesByIdsAsync(chunk, ct))
            {
                if (!byId.TryGetValue(item.Fixture.Id, out var fixture))
                {
                    continue;
                }

                ApiMapper.Apply(fixture, item, leagues.GetValueOrDefault(fixture.LeagueId)?.Type, now);
                live += fixture.IsLive ? 1 : 0;
                finished += fixture.IsFinished ? 1 : 0;
                if (fixture.IsLive || fixture.IsFinished)
                {
                    ApplyStats(stats, fixture, item, now);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return finished == 0 ? null : $"Trực tiếp: {live} trận đang đá, {finished} trận vừa kết thúc.";
    }

    /// <summary>
    /// Thống kê các trận đã đá (mùa hiện tại và mùa trước), ưu tiên trận gần nhất. Mỗi request lấy 20 trận.
    /// Trận đã có thống kê cuối cùng thì bỏ qua; trận không có thống kê sau 1 ngày thì đánh dấu để khỏi lấy lại.
    /// </summary>
    public async Task<string?> SyncStatsAsync(int? maxRequests, CancellationToken ct)
    {
        var o = options.Value;
        var now = Now;
        var settledBefore = now.AddHours(-3);
        var leagues = await db.Leagues.AsNoTracking().Where(l => l.IsTracked && !l.IsDemo && l.HasStatsCoverage).ToListAsync(ct);
        var limit = maxRequests ?? o.StatsMaxRequests;
        var pending = new List<(int Id, DateTime Kickoff)>();
        foreach (var league in leagues)
        {
            var oldest = league.CurrentSeason - o.StatsPreviousSeasons;
            pending.AddRange((await db.Fixtures.AsNoTracking()
                    .Where(f => f.LeagueId == league.Id && f.Season >= oldest && f.Season <= league.CurrentSeason
                        && f.KickoffUtc <= settledBefore
                        && (f.Status == "FT" || f.Status == "AET" || f.Status == "PEN")
                        && !db.FixtureStats.Any(s => s.FixtureId == f.Id && s.IsFinal))
                    .OrderByDescending(f => f.KickoffUtc)
                    .Select(f => new { f.Id, f.KickoffUtc })
                    .Take(limit * 20)
                    .ToListAsync(ct))
                .Select(f => (f.Id, f.KickoffUtc)));
        }

        if (pending.Count == 0)
        {
            return null;
        }

        var batch = pending.OrderByDescending(p => p.Kickoff).Take(limit * 20).Select(p => p.Id).ToList();
        var leagueTypes = leagues.ToDictionary(l => l.Id, l => l.Type);
        var fixtures = await db.Fixtures.Where(f => batch.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);
        var stats = await db.FixtureStats.Where(s => batch.Contains(s.FixtureId)).ToDictionaryAsync(s => s.FixtureId, ct);
        var withStats = 0;
        var requests = 0;
        foreach (var chunk in batch.Chunk(20))
        {
            if (requests > 0 && QuotaLow(o))
            {
                break;
            }

            requests++;
            var items = await api.GetFixturesByIdsAsync(chunk, ct);
            var returned = new HashSet<int>();
            foreach (var item in items)
            {
                if (!fixtures.TryGetValue(item.Fixture.Id, out var fixture))
                {
                    continue;
                }

                returned.Add(fixture.Id);
                ApiMapper.Apply(fixture, item, leagueTypes.GetValueOrDefault(fixture.LeagueId), now);
                var row = ApplyStats(stats, fixture, item, now);
                if (row.HomeCorners.HasValue)
                {
                    withStats++;
                }

                // Có thống kê, hoặc đã qua 1 ngày mà API vẫn không có: không lấy lại nữa.
                row.IsFinal = row.HomeCorners.HasValue || fixture.KickoffUtc < now.AddDays(-1);
            }

            // Trận API không trả về (bị xóa/đổi id): đánh dấu để khỏi hỏi lại mãi.
            foreach (var id in chunk.Where(id => !returned.Contains(id)))
            {
                var row = stats.GetValueOrDefault(id) ?? AddStats(stats, id, now);
                row.IsFinal = true;
            }

            await db.SaveChangesAsync(ct);
        }

        var remaining = pending.Count - batch.Count;
        return $"Thống kê trận: {batch.Count} trận ({withStats} có số liệu góc/thẻ), {requests} request."
            + (remaining > 0 ? $" Còn ít nhất {remaining} trận chờ tải." : "");
    }

    /// <summary>
    /// HLV hiện tại của các đội có trận trong 3 ngày tới (mỗi đội kiểm tra tối đa 1 lần/tuần).
    /// HLV nhận việc trong vòng 180 ngày được ghi thành "HLV mới" để áp quy tắc chờ ổn định.
    /// </summary>
    public async Task<string?> SyncCoachesAsync(CancellationToken ct)
    {
        var now = Now;
        var checkedAfter = now.AddDays(-7);
        var teamIds = (await db.Fixtures.AsNoTracking()
                .Where(f => f.KickoffUtc > now && f.KickoffUtc <= now.AddDays(3) && f.League!.IsTracked && !f.League.IsDemo)
                .Select(f => new { f.HomeTeamId, f.AwayTeamId })
                .ToListAsync(ct))
            .SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId })
            .Distinct()
            .ToList();
        var teams = await db.Teams
            .Where(t => teamIds.Contains(t.Id) && (t.CoachCheckedAtUtc == null || t.CoachCheckedAtUtc < checkedAfter))
            .Take(150)
            .ToListAsync(ct);
        if (teams.Count == 0)
        {
            return null;
        }

        var added = new List<string>();
        foreach (var team in teams)
        {
            if (QuotaLow(options.Value))
            {
                break;
            }

            var coach = CurrentCoach(await api.GetCoachesAsync(team.Id, ct), team.Id);
            team.CoachCheckedAtUtc = now;
            if (coach is { } c && c.Start >= now.AddDays(-180))
            {
                var exists = await db.TeamChanges.AnyAsync(x => x.TeamId == team.Id && x.Kind == TeamChangeKind.NewManager && x.PersonName == c.Name, ct);
                if (!exists)
                {
                    db.TeamChanges.Add(new TeamChange
                    {
                        TeamId = team.Id,
                        Kind = TeamChangeKind.NewManager,
                        PersonName = c.Name,
                        StartDate = c.Start,
                        Source = TeamChangeSource.Api,
                        UpdatedAtUtc = now,
                    });
                    added.Add($"{team.Name}: {c.Name} ({c.Start:dd/MM/yyyy})");
                }
            }

            await db.SaveChangesAsync(ct);
        }

        return $"HLV: kiểm tra {teams.Count} đội." + (added.Count > 0 ? $" HLV mới: {string.Join("; ", added)}." : "");
    }

    /// <summary>HLV đang dẫn dắt đội (mục sự nghiệp của đội chưa có ngày kết thúc, bắt đầu gần nhất).</summary>
    public static (string Name, DateTime Start)? CurrentCoach(IEnumerable<CoachItem> coaches, int teamId)
    {
        (string Name, DateTime Start)? best = null;
        foreach (var coach in coaches)
        {
            foreach (var career in coach.Career.Where(c => c.Team.Id == teamId && string.IsNullOrEmpty(c.End)))
            {
                if (DateTime.TryParse(career.Start, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var start)
                    && (best is null || start > best.Value.Start))
                {
                    best = (coach.Name, start);
                }
            }
        }

        return best;
    }

    /// <summary>Danh sách vắng mặt của các trận trong 24 giờ tới (thay toàn bộ danh sách cũ của trận).</summary>
    public async Task<string?> SyncInjuriesAsync(CancellationToken ct)
    {
        var now = Now;
        var ids = await db.Fixtures.AsNoTracking()
            .Where(f => f.KickoffUtc > now && f.KickoffUtc <= now.AddHours(24) && (f.Status == "NS" || f.Status == "TBD")
                && f.League!.IsTracked && !f.League.IsDemo && f.League.HasInjuriesCoverage)
            .OrderBy(f => f.KickoffUtc)
            .Select(f => f.Id)
            .ToListAsync(ct);
        if (ids.Count == 0)
        {
            return null;
        }

        var players = 0;
        foreach (var id in ids)
        {
            if (QuotaLow(options.Value))
            {
                break;
            }

            var items = await api.GetInjuriesAsync(id, ct);
            await db.FixtureInjuries.Where(i => i.FixtureId == id).ExecuteDeleteAsync(ct);
            foreach (var item in items.DistinctBy(i => (i.Team.Id, i.Player.Id, i.Player.Name)))
            {
                db.FixtureInjuries.Add(new FixtureInjury
                {
                    FixtureId = id,
                    TeamId = item.Team.Id,
                    PlayerName = Truncate(item.Player.Name, 150),
                    Type = Truncate(item.Player.Type ?? "", 50),
                    Reason = item.Player.Reason is null ? null : Truncate(item.Player.Reason, 200),
                });
                players++;
            }

            await db.SaveChangesAsync(ct);
        }

        return $"Chấn thương/treo giò: {ids.Count} trận, {players} cầu thủ.";
    }

    /// <summary>Còn quá ít request trong ngày: dừng các tác vụ phụ để dành cho kèo và trực tiếp.</summary>
    public static bool QuotaLow(SyncOptions o) => ApiFootballClient.RemainingToday is { } r && r < o.QuotaReserve;

    private FixtureStats ApplyStats(Dictionary<int, FixtureStats> stats, Fixture fixture, FixtureItem item, DateTime now)
    {
        var row = stats.GetValueOrDefault(fixture.Id) ?? AddStats(stats, fixture.Id, now);
        ApiMapper.ApplyStats(row, item, now);
        return row;
    }

    private FixtureStats AddStats(Dictionary<int, FixtureStats> stats, int fixtureId, DateTime now)
    {
        var row = new FixtureStats { FixtureId = fixtureId, UpdatedAtUtc = now };
        db.FixtureStats.Add(row);
        stats[fixtureId] = row;
        return row;
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

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
