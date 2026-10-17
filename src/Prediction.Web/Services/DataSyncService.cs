using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prediction.Core.Engine.LiveCorners;
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

    /// <summary>Dò đội hình ra sân (khi có đội hình thì lấy kèo ngay): chu kỳ và khoảng trước giờ bóng lăn.</summary>
    public int LineupsIntervalMinutes { get; set; } = 5;
    public int LineupsWindowMinutes { get; set; } = 75;

    /// <summary>Chụp kèo khoảng <see cref="PreKickoffMinutes"/> phút trước giờ bóng lăn.</summary>
    public int PreKickoffIntervalMinutes { get; set; } = 1;
    public int PreKickoffMinutes { get; set; } = 5;

    public int H2HIntervalMinutes { get; set; } = 360;

    /// <summary>Mỗi cặp đấu chỉ hỏi lại đối đầu sau số ngày này (kể cả khi API trả 0 trận).</summary>
    public int H2HRecheckDays { get; set; } = 7;
    public int VenuesIntervalMinutes { get; set; } = 360;
    public int WeatherIntervalMinutes { get; set; } = 180;

    /// <summary>Kèo live (chấp, tài xỉu, góc) từ odds/live — chỉ gọi API khi có trận theo dõi đang đá.</summary>
    public int LiveOddsIntervalMinutes { get; set; } = 2;

    /// <summary>Góc Live: chốt/chấm khuyến nghị góc (không gọi API; kèo góc lấy trong tác vụ kèo live).</summary>
    public int LiveCornersIntervalMinutes { get; set; } = 2;

    /// <summary>AI tự điền thông tin bên lề cho trận nhóm A/B (chỉ khi có Anthropic:ApiKey).</summary>
    public int SideInfoIntervalMinutes { get; set; } = 30;

    /// <summary>Còn ít hơn số request này trong ngày thì tạm dừng các tác vụ phụ (thống kê, HLV, chấn thương, tư vấn).</summary>
    public int QuotaReserve { get; set; } = 1000;

    /// <summary>
    /// Góc Live nhanh: khi có trận ở vùng nóng quanh cửa sổ vàng thì cứ chừng này giây lấy lại số liệu + kèo góc live và chốt khuyến nghị
    /// (≈ 2 request/lần). 0 = tắt, chỉ dùng nhịp thường (<see cref="LiveIntervalMinutes"/>).
    /// </summary>
    public int LiveCornerFastSeconds { get; set; } = 30;

    public int LiveCornerHotH1From { get; set; } = 22;
    public int LiveCornerHotH1To { get; set; } = 31;
    public int LiveCornerHotH2From { get; set; } = 72;
    public int LiveCornerHotH2To { get; set; } = 81;

    /// <summary>Còn ít hơn số request này trong ngày thì Góc Live nhanh tự tạm dừng (quay về nhịp thường).</summary>
    public int LiveCornerFastMinRemaining { get; set; } = 1500;

    /// <summary>
    /// Kèo live gọi chung (odds/live) thiếu trận nào thì gọi riêng theo giải, tối đa chừng này giải mỗi lần.
    /// Pro chỉ gọi bù cho trận gần cửa sổ góc; <see cref="LiveOddsFallbackAllLive"/> = true thì gọi bù cho mọi trận đang đá.
    /// </summary>
    public int LiveOddsFallbackMaxLeagues { get; set; } = 8;
    public bool LiveOddsFallbackAllLive { get; set; }

    public HotZone HotZone => new(LiveCornerHotH1From, LiveCornerHotH1To, LiveCornerHotH2From, LiveCornerHotH2To);

    /// <summary>
    /// Hồ sơ đồng bộ: "Auto" (mặc định) dùng giá trị <see cref="Ultra"/> khi giới hạn ngày của gói API ≥ <see cref="SyncUltraOptions.MinDailyLimit"/>,
    /// ngược lại dùng các giá trị gốc (cho gói Pro 7.500/ngày); "Pro" / "Ultra" ép cố định một hồ sơ.
    /// </summary>
    public string Plan { get; set; } = "Auto";

    public SyncUltraOptions Ultra { get; set; } = new();

    public bool UsesUltra(int? dailyLimit) => Plan.Trim().ToLowerInvariant() switch
    {
        "ultra" => true,
        "pro" => false,
        _ => dailyLimit is { } l && l >= Ultra.MinDailyLimit,
    };

    /// <summary>Cấu hình áp dụng với giới hạn ngày này: chính nó nếu là hồ sơ Pro, hoặc bản sao đã thay các giá trị Ultra.</summary>
    public SyncOptions For(int? dailyLimit)
    {
        if (!UsesUltra(dailyLimit))
        {
            return this;
        }

        var o = (SyncOptions)MemberwiseClone();
        o.OddsIntervalMinutes = Ultra.OddsIntervalMinutes;
        o.LiveIntervalMinutes = Ultra.LiveIntervalMinutes;
        o.LiveOddsIntervalMinutes = Ultra.LiveOddsIntervalMinutes;
        o.LiveCornersIntervalMinutes = Ultra.LiveCornersIntervalMinutes;
        o.LiveCornerFastSeconds = Ultra.LiveCornerFastSeconds;
        o.StatsMaxRequests = Ultra.StatsMaxRequests;
        o.QuotaReserve = Ultra.QuotaReserve;
        o.LiveCornerFastMinRemaining = Ultra.LiveCornerFastMinRemaining;
        o.LiveOddsFallbackMaxLeagues = Ultra.LiveOddsFallbackMaxLeagues;
        o.LiveOddsFallbackAllLive = Ultra.LiveOddsFallbackAllLive;
        return o;
    }

    /// <summary>Cấu hình đang áp dụng theo giới hạn ngày đọc từ header của request API gần nhất.</summary>
    public SyncOptions Effective() => For(ApiFootballClient.DailyLimit);

    public string PlanName(int? dailyLimit) => UsesUltra(dailyLimit) ? "Ultra" : "Pro";
}

/// <summary>Giá trị thay thế khi dùng gói lớn (API-Football Ultra 75.000 request/ngày, ~25–35 nghìn request/ngày với 19–30 giải).</summary>
public sealed class SyncUltraOptions
{
    /// <summary>Hồ sơ "Auto" bật Ultra khi giới hạn ngày (header x-ratelimit-requests-limit) ≥ số này.</summary>
    public int MinDailyLimit { get; set; } = 50000;

    public int OddsIntervalMinutes { get; set; } = 20;
    public int LiveIntervalMinutes { get; set; } = 1;
    public int LiveOddsIntervalMinutes { get; set; } = 1;
    public int LiveCornersIntervalMinutes { get; set; } = 1;
    public int LiveCornerFastSeconds { get; set; } = 20;
    public int StatsMaxRequests { get; set; } = 200;
    public int QuotaReserve { get; set; } = 5000;
    public int LiveCornerFastMinRemaining { get; set; } = 3000;
    public int LiveOddsFallbackMaxLeagues { get; set; } = 30;
    public bool LiveOddsFallbackAllLive { get; set; } = true;
}

/// <summary>Các tác vụ lấy dữ liệu từ API-Football và ghi vào CSDL.</summary>
public sealed class DataSyncService(
    AppDbContext db,
    ApiFootballClient api,
    OpenMeteoClient openMeteo,
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
    public Task<string?> SyncLiveAsync(CancellationToken ct) => SyncLiveAsync(null, ct);

    /// <param name="onlyIds">Chỉ lấy các trận này (Góc Live nhanh: trận ở vùng nóng); null = mọi trận đang đá.</param>
    public async Task<string?> SyncLiveAsync(IReadOnlyCollection<int>? onlyIds, CancellationToken ct)
    {
        var now = Now;
        var leagues = await db.Leagues.AsNoTracking().Where(l => l.IsTracked && !l.IsDemo).ToDictionaryAsync(l => l.Id, ct);
        var leagueIds = leagues.Keys.ToList();
        var windowStart = now.AddHours(-4);
        var candidates = (await db.Fixtures
                .Where(f => leagueIds.Contains(f.LeagueId) && f.KickoffUtc <= now && f.KickoffUtc >= windowStart)
                .ToListAsync(ct))
            .Where(f => !f.IsFinished && !f.IsCancelled && f.Status != "PST" && (onlyIds is null || onlyIds.Contains(f.Id)))
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
                    var row = ApplyStats(stats, fixture, item, now);
                    if (row.HomeCorners.HasValue)
                    {
                        if (fixture.Status == "HT" && row.FirstHalfHomeCorners is null)
                        {
                            row.FirstHalfHomeCorners = row.HomeCorners;
                            row.FirstHalfAwayCorners = row.AwayCorners;
                        }

                        if (fixture.IsLive)
                        {
                            db.LiveStatSnapshots.Add(Snapshot(fixture, row, now));
                        }
                    }
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
        var limit = maxRequests ?? o.Effective().StatsMaxRequests;
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

    /// <summary>
    /// Dò đội hình ra sân của các trận sắp bóng lăn (20 trận/request). Trận vừa có đội hình thì lưu đội hình,
    /// lấy kèo ngay và chụp mốc "khi có đội hình".
    /// </summary>
    public async Task<string?> SyncLineupsAsync(CancellationToken ct)
    {
        var now = Now;
        var until = now.AddMinutes(options.Value.LineupsWindowMinutes);
        var ids = await db.Fixtures.AsNoTracking()
            .Where(f => f.KickoffUtc > now && f.KickoffUtc <= until && (f.Status == "NS" || f.Status == "TBD")
                && f.League!.IsTracked && !f.League.IsDemo && !db.FixtureLineups.Any(l => l.FixtureId == f.Id))
            .Select(f => f.Id)
            .ToListAsync(ct);
        if (ids.Count == 0)
        {
            return null;
        }

        var found = new List<int>();
        foreach (var chunk in ids.Chunk(20))
        {
            foreach (var item in await api.GetFixturesByIdsAsync(chunk, ct))
            {
                var lineup = new FixtureLineup();
                if (ApiMapper.ApplyLineup(lineup, item, now))
                {
                    db.FixtureLineups.Add(lineup);
                    found.Add(item.Fixture.Id);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        foreach (var id in found)
        {
            var odds = await api.GetFixtureOddsAsync(id, ct);
            await StoreOddsAsync(odds, ct);
            await CheckpointAsync(id, OddsCheckpointKind.Lineups, odds, ct);
        }

        return found.Count == 0 ? null : $"Đội hình: {found.Count} trận vừa có đội hình, đã chụp kèo.";
    }

    /// <summary>Chụp kèo ~5 phút trước giờ bóng lăn cho từng trận.</summary>
    public async Task<string?> SyncPreKickoffOddsAsync(CancellationToken ct)
    {
        var now = Now;
        var minutes = options.Value.PreKickoffMinutes;
        var from = now.AddMinutes(Math.Max(1, minutes - 3));
        var until = now.AddMinutes(minutes + 3);
        var ids = await db.Fixtures.AsNoTracking()
            .Where(f => f.KickoffUtc > from && f.KickoffUtc <= until && (f.Status == "NS" || f.Status == "TBD")
                && f.League!.IsTracked && !f.League.IsDemo
                && !db.OddsCheckpoints.Any(c => c.FixtureId == f.Id && c.Kind == OddsCheckpointKind.PreKickoff))
            .Select(f => f.Id)
            .ToListAsync(ct);
        foreach (var id in ids)
        {
            var odds = await api.GetFixtureOddsAsync(id, ct);
            await StoreOddsAsync(odds, ct);
            await CheckpointAsync(id, OddsCheckpointKind.PreKickoff, odds, ct);
        }

        return ids.Count == 0 ? null : $"Kèo {minutes} phút trước trận: {ids.Count} trận.";
    }

    /// <summary>Ghi mốc kèo (giữ bản đầu tiên). Không có kèo trong phản hồi thì dùng bản ghi kèo gần nhất trong CSDL.</summary>
    private async Task CheckpointAsync(int fixtureId, OddsCheckpointKind kind, IReadOnlyCollection<OddsItem> items, CancellationToken ct)
    {
        if (await db.OddsCheckpoints.AnyAsync(c => c.FixtureId == fixtureId && c.Kind == kind, ct))
        {
            return;
        }

        var item = items.FirstOrDefault(i => i.Fixture.Id == fixtureId);
        var quotes = item is null ? [] : OddsParser.Parse(item, api.BookmakerId);
        async Task<(decimal? Line, decimal? A, decimal? B)> Quote(Market market)
        {
            if (quotes.FirstOrDefault(q => q.Market == market) is { } q)
            {
                return (q.Line, q.PriceA, q.PriceB);
            }

            var last = await db.OddsSnapshots.AsNoTracking()
                .Where(x => x.FixtureId == fixtureId && x.Market == market)
                .OrderByDescending(x => x.FetchedAtUtc).ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct);
            return (last?.Line, last?.PriceA, last?.PriceB);
        }

        var ah = await Quote(Market.AsianHandicap);
        var ou = await Quote(Market.OverUnder);
        if (ah.Line is null && ou.Line is null)
        {
            return;
        }

        db.OddsCheckpoints.Add(new OddsCheckpoint
        {
            FixtureId = fixtureId,
            Kind = kind,
            AtUtc = Now,
            BookmakerUpdatedAtUtc = item?.Update?.UtcDateTime,
            HandicapLine = ah.Line,
            HandicapHome = ah.A,
            HandicapAway = ah.B,
            TotalLine = ou.Line,
            Over = ou.A,
            Under = ou.B,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>5 lần đối đầu gần nhất của các cặp đấu trong 3 ngày tới (mỗi cặp lấy lại tối đa 1 lần/tuần).</summary>
    public async Task<string?> SyncHeadToHeadAsync(CancellationToken ct)
    {
        var now = Now;
        var pairs = (await db.Fixtures.AsNoTracking()
                .Where(f => f.KickoffUtc > now && f.KickoffUtc <= now.AddDays(3) && f.League!.IsTracked && !f.League.IsDemo)
                .Select(f => new { f.HomeTeamId, f.AwayTeamId })
                .ToListAsync(ct))
            .Select(p => (p.HomeTeamId, p.AwayTeamId, Key: H2HMeeting.Key(p.HomeTeamId, p.AwayTeamId)))
            .DistinctBy(p => p.Key)
            .ToList();
        var keys = pairs.Select(p => p.Key).ToList();
        var fresh = await db.H2HChecks.AsNoTracking()
            .Where(c => keys.Contains(c.PairKey) && c.CheckedAtUtc > now.AddDays(-options.Value.H2HRecheckDays))
            .Select(c => c.PairKey)
            .ToListAsync(ct);
        var todo = pairs.Where(p => !fresh.Contains(p.Key)).Take(100).ToList();
        var meetings = 0;
        foreach (var (home, away, _) in todo)
        {
            if (QuotaLow(options.Value))
            {
                break;
            }

            meetings += await FetchHeadToHeadAsync(home, away, ct);
        }

        return todo.Count == 0 ? null : $"Đối đầu: {todo.Count} cặp, {meetings} trận.";
    }

    /// <summary>Lấy (và lưu) 5 lần đối đầu gần nhất của một cặp đội. Trả về số trận nhận được.</summary>
    public async Task<int> FetchHeadToHeadAsync(int teamA, int teamB, CancellationToken ct)
    {
        var key = H2HMeeting.Key(teamA, teamB);
        var items = (await api.GetHeadToHeadAsync(teamA, teamB, 5, ct))
            .Where(i => FixtureStatus.IsFinished(i.Fixture.Status.Short))
            .ToList();
        var ids = items.Select(i => i.Fixture.Id).ToList();
        var existing = await db.H2HMeetings.Where(m => ids.Contains(m.FixtureId)).ToDictionaryAsync(m => m.FixtureId, ct);
        foreach (var item in items)
        {
            var meeting = ApiMapper.ToMeeting(item);
            if (existing.TryGetValue(meeting.FixtureId, out var row))
            {
                db.Entry(row).CurrentValues.SetValues(meeting);
            }
            else
            {
                db.H2HMeetings.Add(meeting);
            }
        }

        var check = await db.H2HChecks.FindAsync([key], ct);
        if (check is null)
        {
            db.H2HChecks.Add(new H2HCheck { PairKey = key, CheckedAtUtc = Now });
        }
        else
        {
            check.CheckedAtUtc = Now;
        }

        await db.SaveChangesAsync(ct);
        return items.Count;
    }

    /// <summary>Thông tin sân (mặt sân, sức chứa) của các trận trong 3 ngày tới, rồi tọa độ + độ cao qua Open-Meteo.</summary>
    public async Task<string?> SyncVenuesAsync(CancellationToken ct)
    {
        var now = Now;
        var venueIds = await db.Fixtures.AsNoTracking()
            .Where(f => f.VenueId > 0 && f.KickoffUtc > now.AddHours(-3) && f.KickoffUtc <= now.AddDays(3) && f.League!.IsTracked)
            .Select(f => f.VenueId!.Value)
            .Distinct()
            .ToListAsync(ct);
        var known = await db.Venues.Where(v => venueIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var added = 0;
        foreach (var id in venueIds.Where(id => !known.ContainsKey(id)).Take(60))
        {
            if (QuotaLow(options.Value))
            {
                break;
            }

            if (await api.GetVenueAsync(id, ct) is { } item)
            {
                var venue = new Venue();
                ApiMapper.Apply(venue, item, now);
                db.Venues.Add(venue);
                known[id] = venue;
                added++;
            }
        }

        await db.SaveChangesAsync(ct);

        var geocoded = 0;
        if (openMeteo.Enabled)
        {
            foreach (var venue in known.Values.Where(v => v.GeoCheckedAtUtc is null && !string.IsNullOrWhiteSpace(v.City)))
            {
                venue.GeoCheckedAtUtc = now;
                if (await openMeteo.GeocodeAsync(venue.City!, venue.Country, ct) is { } point)
                {
                    venue.Latitude = point.Latitude;
                    venue.Longitude = point.Longitude;
                    venue.ElevationM = point.ElevationM;
                    venue.GeoSource = "Open-Meteo";
                    geocoded++;
                }
            }

            await db.SaveChangesAsync(ct);
        }

        return added == 0 && geocoded == 0 ? null : $"Sân: thêm {added} sân, định vị {geocoded} sân.";
    }

    /// <summary>Dự báo thời tiết giờ bóng lăn (Open-Meteo) cho các trận trong 48 giờ tới.</summary>
    public async Task<string?> SyncWeatherAsync(CancellationToken ct)
    {
        if (!openMeteo.Enabled)
        {
            return null;
        }

        var now = Now;
        var rows = await db.Fixtures.AsNoTracking()
            .Where(f => f.KickoffUtc > now && f.KickoffUtc <= now.AddHours(48) && (f.Status == "NS" || f.Status == "TBD")
                && f.League!.IsTracked && !f.League.IsDemo && f.VenueId != null)
            .Join(db.Venues.Where(v => v.Latitude != null && v.Longitude != null), f => f.VenueId, v => v.Id,
                (f, v) => new { f.Id, f.KickoffUtc, Lat = v.Latitude!.Value, Lon = v.Longitude!.Value })
            .ToListAsync(ct);
        if (rows.Count == 0)
        {
            return null;
        }

        var ids = rows.Select(r => r.Id).ToList();
        var existing = await db.FixtureWeathers.Where(w => ids.Contains(w.FixtureId)).ToDictionaryAsync(w => w.FixtureId, ct);
        var updated = 0;
        foreach (var r in rows)
        {
            if (await openMeteo.ForecastAsync(r.Lat, r.Lon, r.KickoffUtc, ct) is not { } w)
            {
                continue;
            }

            if (!existing.TryGetValue(r.Id, out var row))
            {
                row = new FixtureWeather { FixtureId = r.Id };
                db.FixtureWeathers.Add(row);
            }

            row.TemperatureC = w.TemperatureC;
            row.HumidityPercent = w.HumidityPercent;
            row.PrecipitationMm = w.PrecipitationMm;
            row.PrecipitationProbability = w.PrecipitationProbability;
            row.WindKph = w.WindKph;
            row.WeatherCode = w.WeatherCode;
            row.Source = "Open-Meteo";
            row.FetchedAtUtc = now;
            updated++;
        }

        await db.SaveChangesAsync(ct);
        return updated == 0 ? null : $"Thời tiết: {updated} trận.";
    }

    private static LiveStatSnapshot Snapshot(Fixture f, FixtureStats s, DateTime now) => new()
    {
        FixtureId = f.Id,
        Minute = f.Status == "HT" ? 45 : f.Elapsed ?? 0,
        Status = f.Status,
        CapturedAtUtc = now,
        HomeGoals = f.HomeGoals ?? 0,
        AwayGoals = f.AwayGoals ?? 0,
        HomeCorners = s.HomeCorners ?? 0,
        AwayCorners = s.AwayCorners ?? 0,
        HomeShots = s.HomeShots,
        AwayShots = s.AwayShots,
        HomeOnTarget = s.HomeShotsOnTarget,
        AwayOnTarget = s.AwayShotsOnTarget,
        HomeBlocked = s.HomeBlocked,
        AwayBlocked = s.AwayBlocked,
        HomeRed = s.HomeRed ?? 0,
        AwayRed = s.AwayRed ?? 0,
    };

    /// <summary>
    /// Kèo live từ odds/live (1 request cho mọi trận đang đá), chỉ gọi khi có trận theo dõi đang đá:
    /// kèo chấp/tài xỉu bàn (cả trận; hiệp 1 khi đang hiệp 1) cho mọi trận, kèo góc cho trận gần cửa sổ vàng của Góc Live
    /// (hiệp 1 phút 20–32, hiệp 2 phút 70–82). Chỉ ghi khi mức/odds đổi.
    /// </summary>
    public Task<(int Goals, int Corners)> SyncLiveOddsAsync(CancellationToken ct) => SyncLiveOddsAsync(null, ct);

    /// <param name="onlyIds">Chỉ các trận này (Góc Live nhanh: trận ở vùng nóng); null = mọi trận đang đá.</param>
    public async Task<(int Goals, int Corners)> SyncLiveOddsAsync(IReadOnlyCollection<int>? onlyIds, CancellationToken ct)
    {
        var now = Now;
        var sync = options.Value.Effective();
        var live = await db.Fixtures.AsNoTracking()
            .Where(f => f.League!.IsTracked && !f.League.IsDemo && f.KickoffUtc <= now && f.KickoffUtc >= now.AddHours(-3)
                && (f.Status == "1H" || f.Status == "HT" || f.Status == "2H")
                && (onlyIds == null || onlyIds.Contains(f.Id)))
            .ToDictionaryAsync(f => f.Id, ct);
        if (live.Count == 0)
        {
            return (0, 0);
        }

        // Phút ước lượng (phút API + thời gian đã trôi) để không sót kèo khi số liệu phút còn cũ.
        bool NearCornerWindow(Fixture f) => LiveClock.EstimatedMinute(f.Status, f.Elapsed, f.UpdatedAtUtc, now) is { } m
            && (f.Status == "1H" && m is >= 18 and <= 33 || f.Status == "2H" && m is >= 68 and <= 83);

        var ids = live.Keys.ToList();
        var latestGoals = (await db.LiveOddsSnapshots.AsNoTracking()
                .Where(o => ids.Contains(o.FixtureId))
                .GroupBy(o => new { o.FixtureId, o.Market, o.FirstHalf })
                .Select(g => g.OrderByDescending(o => o.FetchedAtUtc).ThenByDescending(o => o.Id).First())
                .ToListAsync(ct))
            .ToDictionary(o => (o.FixtureId, o.Market, o.FirstHalf));
        var latestCorners = (await db.LiveCornerOdds.AsNoTracking()
                .Where(o => ids.Contains(o.FixtureId))
                .GroupBy(o => new { o.FixtureId, o.Window })
                .Select(g => g.OrderByDescending(o => o.FetchedAtUtc).ThenByDescending(o => o.Id).First())
                .ToListAsync(ct))
            .ToDictionary(o => (o.FixtureId, o.Window));

        // Gọi chung trước (1 request); trận nào thiếu thì gọi bù theo giải.
        var items = await api.GetLiveOddsAsync(null, ct);
        var returned = items.Select(i => i.Fixture.Id).ToHashSet();
        var fallback = LiveOddsFallbackLeagues(
            live.Values.Select(f => (f.Id, f.LeagueId, Wanted: f.Status != "HT" && (sync.LiveOddsFallbackAllLive || NearCornerWindow(f)))),
            returned, sync.LiveOddsFallbackMaxLeagues);
        foreach (var leagueId in fallback)
        {
            if (QuotaLow(sync) && !live.Values.Any(f => f.LeagueId == leagueId && NearCornerWindow(f)))
            {
                continue; // quota thấp: chỉ gọi bù cho giải có trận gần cửa sổ góc
            }

            items.AddRange((await api.GetLiveOddsAsync(leagueId, ct)).Where(i => returned.Add(i.Fixture.Id)));
        }

        int goals = 0, corners = 0;
        foreach (var item in items)
        {
            if (!live.TryGetValue(item.Fixture.Id, out var f))
            {
                continue;
            }

            foreach (var q in LiveOddsParser.ParseGoals(item).Where(q => !q.FirstHalf || f.Status == "1H"))
            {
                var key = (q.FixtureId, q.Market, q.FirstHalf);
                if (latestGoals.TryGetValue(key, out var last) && last.SameQuote(q.Line, q.PriceA, q.PriceB))
                {
                    continue;
                }

                var row = new LiveOddsSnapshot
                {
                    FixtureId = q.FixtureId,
                    Market = q.Market,
                    FirstHalf = q.FirstHalf,
                    Line = q.Line,
                    PriceA = q.PriceA,
                    PriceB = q.PriceB,
                    Minute = q.Minute ?? f.Elapsed,
                    HomeGoals = q.HomeGoals ?? f.HomeGoals ?? 0,
                    AwayGoals = q.AwayGoals ?? f.AwayGoals ?? 0,
                    FetchedAtUtc = now,
                };
                db.LiveOddsSnapshots.Add(row);
                latestGoals[key] = row;
                goals++;
            }

            if (!NearCornerWindow(f))
            {
                continue;
            }

            foreach (var q in LiveOddsParser.Parse(item))
            {
                if (latestCorners.TryGetValue((q.FixtureId, q.Window), out var last) && last.Line == q.Line && last.OverOdds == q.OverOdds && last.UnderOdds == q.UnderOdds)
                {
                    continue;
                }

                var row = new LiveCornerOdds { FixtureId = q.FixtureId, Window = q.Window, Line = q.Line, OverOdds = q.OverOdds, UnderOdds = q.UnderOdds, Minute = q.Minute, FetchedAtUtc = now };
                db.LiveCornerOdds.Add(row);
                latestCorners[(q.FixtureId, q.Window)] = row;
                corners++;
            }
        }

        await db.SaveChangesAsync(ct);
        return (goals, corners);
    }

    /// <summary>
    /// Các giải cần gọi kèo live riêng: có trận cần kèo (<c>Wanted</c>) mà lần gọi chung không trả về.
    /// Giải thiếu nhiều trận nhất trước, tối đa <paramref name="maxLeagues"/> giải.
    /// </summary>
    public static List<int> LiveOddsFallbackLeagues(IEnumerable<(int Id, int LeagueId, bool Wanted)> live, IReadOnlySet<int> returned, int maxLeagues) =>
        live.Where(f => f.Wanted && !returned.Contains(f.Id))
            .GroupBy(f => f.LeagueId)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Take(Math.Max(0, maxLeagues))
            .Select(g => g.Key)
            .ToList();

    /// <summary>Còn quá ít request trong ngày: dừng các tác vụ phụ để dành cho kèo và trực tiếp.</summary>
    public static bool QuotaLow(SyncOptions o) => ApiFootballClient.RemainingToday is { } r && r < o.Effective().QuotaReserve;

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
