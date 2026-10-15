using System.Globalization;
using System.Text.Json;
using Prediction.Core.Entities;

namespace Prediction.Web.ApiFootball;

/// <summary>Chuyển dữ liệu API-Football sang entity.</summary>
public static class ApiMapper
{
    /// <summary>
    /// Trận có thuộc giai đoạn có BXH không. Vòng đấu thường ("Regular Season - 12") và league phase C1
    /// ("League Stage - 3") có; play-off, knock-out, vòng bảng C1 cũ (nhiều bảng gộp chung) thì không.
    /// </summary>
    public static bool HasTable(string? leagueType, string? round)
    {
        if (string.IsNullOrWhiteSpace(round))
        {
            return string.Equals(leagueType, "League", StringComparison.OrdinalIgnoreCase);
        }

        if (round.Contains("Regular Season", StringComparison.OrdinalIgnoreCase) ||
            round.StartsWith("League Stage", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(leagueType, "League", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] notTable = ["Play", "Relegation", "Final", "Championship", "Round of", "Qualif", "Group", "Knockout"];
        return !notTable.Any(k => round.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    public static void Apply(Fixture target, FixtureItem item, string? leagueType, DateTime nowUtc)
    {
        target.Id = item.Fixture.Id;
        target.LeagueId = item.League.Id;
        target.Season = item.League.Season;
        target.Round = item.League.Round;
        target.KickoffUtc = item.Fixture.Date.UtcDateTime;
        target.Status = string.IsNullOrWhiteSpace(item.Fixture.Status.Short) ? FixtureStatus.NotStarted : item.Fixture.Status.Short;
        target.HomeTeamId = item.Teams.Home.Id;
        target.AwayTeamId = item.Teams.Away.Id;
        target.HasTable = HasTable(leagueType, item.League.Round);

        // Kèo chấp/tài xỉu chấm theo 90 phút: ưu tiên tỉ số "fulltime", không tính hiệp phụ/luân lưu.
        var ft = item.Score?.Fulltime;
        target.HomeGoals = ft?.Home ?? item.Goals.Home;
        target.AwayGoals = ft?.Away ?? item.Goals.Away;
        target.Elapsed = FixtureStatus.IsLive(target.Status) ? item.Fixture.Status.Elapsed : null;
        if (item.Fixture.Venue is { Id: { } venueId } venue)
        {
            target.VenueId = venueId;
            target.VenueName = Truncate(venue.Name, 200);
        }

        target.UpdatedAtUtc = nowUtc;
    }

    /// <summary>Đội hình ra sân; false nếu API chưa công bố đủ đội hình hai đội.</summary>
    public static bool ApplyLineup(FixtureLineup target, FixtureItem item, DateTime nowUtc)
    {
        var lineups = item.Lineups ?? [];
        var home = lineups.FirstOrDefault(l => l.Team.Id == item.Teams.Home.Id);
        var away = lineups.FirstOrDefault(l => l.Team.Id == item.Teams.Away.Id);
        if (home is null || away is null || home.StartXI.Count < 11 || away.StartXI.Count < 11)
        {
            return false;
        }

        target.FixtureId = item.Fixture.Id;
        target.DetectedAtUtc = nowUtc;
        target.HomeFormation = Truncate(home.Formation, 20);
        target.AwayFormation = Truncate(away.Formation, 20);
        target.HomeStartXI = Truncate(string.Join(", ", home.StartXI.Select(p => p.Player.Name)), 1000) ?? "";
        target.AwayStartXI = Truncate(string.Join(", ", away.StartXI.Select(p => p.Player.Name)), 1000) ?? "";
        target.HomeCoach = Truncate(home.Coach?.Name, 150);
        target.AwayCoach = Truncate(away.Coach?.Name, 150);
        return true;
    }

    public static void Apply(Venue target, VenueItem item, DateTime nowUtc)
    {
        target.Id = item.Id;
        target.Name = Truncate(item.Name, 200) ?? "";
        target.City = Truncate(item.City, 150);
        target.Country = Truncate(item.Country, 100);
        target.Capacity = item.Capacity;
        target.Surface = Truncate(item.Surface, 50);
        target.UpdatedAtUtc = nowUtc;
    }

    public static H2HMeeting ToMeeting(FixtureItem item) => new()
    {
        FixtureId = item.Fixture.Id,
        PairKey = H2HMeeting.Key(item.Teams.Home.Id, item.Teams.Away.Id),
        KickoffUtc = item.Fixture.Date.UtcDateTime,
        LeagueName = Truncate(item.League.Name, 200),
        HomeTeamId = item.Teams.Home.Id,
        AwayTeamId = item.Teams.Away.Id,
        HomeGoals = item.Score?.Fulltime?.Home ?? item.Goals.Home,
        AwayGoals = item.Score?.Fulltime?.Away ?? item.Goals.Away,
        HalftimeHome = item.Score?.Halftime?.Home,
        HalftimeAway = item.Score?.Halftime?.Away,
    };

    public static void Apply(ApiAdvice target, PredictionItem item, DateTime nowUtc)
    {
        var p = item.Predictions;
        target.FetchedAtUtc = nowUtc;
        target.WinnerTeamId = p.Winner?.Id;
        target.WinnerComment = Truncate(p.Winner?.Comment, 200);
        target.WinOrDraw = p.WinOrDraw;
        target.UnderOver = Truncate(p.UnderOver, 20);
        target.Advice = Truncate(p.Advice, 500);
        target.PercentHome = Percent(p.Percent?.Home);
        target.PercentDraw = Percent(p.Percent?.Draw);
        target.PercentAway = Percent(p.Percent?.Away);

        var c = item.Comparison;
        target.FormHome = Percent(c?.Form?.Home);
        target.FormAway = Percent(c?.Form?.Away);
        target.AttackHome = Percent(c?.Att?.Home);
        target.AttackAway = Percent(c?.Att?.Away);
        target.DefenceHome = Percent(c?.Def?.Home);
        target.DefenceAway = Percent(c?.Def?.Away);
        target.H2HHome = Percent(c?.H2h?.Home);
        target.H2HAway = Percent(c?.H2h?.Away);
        target.TotalHome = Percent(c?.Total?.Home);
        target.TotalAway = Percent(c?.Total?.Away);
    }

    /// <summary>
    /// Thống kê trận từ phản hồi fixtures?ids=. Trả về false nếu API chưa có thống kê cho trận này.
    /// </summary>
    public static bool ApplyStats(FixtureStats target, FixtureItem item, DateTime nowUtc)
    {
        var homeId = item.Teams.Home.Id;
        var awayId = item.Teams.Away.Id;
        var stats = item.Statistics ?? [];
        var home = stats.FirstOrDefault(s => s.Team.Id == homeId)?.Statistics;
        var away = stats.FirstOrDefault(s => s.Team.Id == awayId)?.Statistics;

        target.HalftimeHome = item.Score?.Halftime?.Home;
        target.HalftimeAway = item.Score?.Halftime?.Away;
        target.GoalTimeline = GoalTimeline(item);
        target.UpdatedAtUtc = nowUtc;

        if (home is null || away is null || home.Count == 0 || away.Count == 0)
        {
            return false;
        }

        // Có bảng thống kê thì giá trị null nghĩa là 0 (vd chưa có thẻ đỏ nào).
        target.HomeCorners = Int(home, "Corner Kicks") ?? 0;
        target.AwayCorners = Int(away, "Corner Kicks") ?? 0;
        target.HomeShots = Int(home, "Total Shots") ?? 0;
        target.AwayShots = Int(away, "Total Shots") ?? 0;
        target.HomeShotsOnTarget = Int(home, "Shots on Goal") ?? 0;
        target.AwayShotsOnTarget = Int(away, "Shots on Goal") ?? 0;
        target.HomeYellow = Int(home, "Yellow Cards") ?? 0;
        target.AwayYellow = Int(away, "Yellow Cards") ?? 0;
        target.HomeRed = Int(home, "Red Cards") ?? 0;
        target.AwayRed = Int(away, "Red Cards") ?? 0;
        target.HomePossession = Percent(Text(home, "Ball Possession"));
        target.AwayPossession = Percent(Text(away, "Ball Possession"));
        target.HomeXg = Dec(home, "expected_goals");
        target.AwayXg = Dec(away, "expected_goals");
        target.HomeBlocked = Int(home, "Blocked Shots") ?? 0;
        target.AwayBlocked = Int(away, "Blocked Shots") ?? 0;
        target.HomeShotsInBox = Int(home, "Shots insidebox") ?? 0;
        target.AwayShotsInBox = Int(away, "Shots insidebox") ?? 0;
        return true;
    }

    /// <summary>
    /// Bàn thắng theo thứ tự "12H,34A". Phản lưới nhà: API có thể ghi theo đội của cầu thủ phản lưới hoặc đội được hưởng,
    /// nên chọn cách gán khớp với tỉ số hiện tại.
    /// </summary>
    public static string? GoalTimeline(FixtureItem item)
    {
        var goals = (item.Events ?? [])
            .Where(e => string.Equals(e.Type, "Goal", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(e.Detail, "Missed Penalty", StringComparison.OrdinalIgnoreCase)
                && e.Time.Elapsed is <= 120)
            .OrderBy(e => e.Time.Elapsed).ThenBy(e => e.Time.Extra ?? 0)
            .ToList();
        if (goals.Count == 0)
        {
            return null;
        }

        string Build(bool invertOwnGoals) => string.Join(",", goals.Select(e =>
        {
            var home = e.Team.Id == item.Teams.Home.Id;
            if (invertOwnGoals && string.Equals(e.Detail, "Own Goal", StringComparison.OrdinalIgnoreCase))
            {
                home = !home;
            }

            var minute = e.Time.Extra is > 0 ? $"{e.Time.Elapsed}+{e.Time.Extra}" : $"{e.Time.Elapsed}";
            return $"{minute}{(home ? 'H' : 'A')}";
        }));

        var asIs = Build(false);
        if (!goals.Any(e => string.Equals(e.Detail, "Own Goal", StringComparison.OrdinalIgnoreCase)))
        {
            return asIs;
        }

        var homeGoals = item.Goals.Home ?? 0;
        return asIs.Count(c => c == 'H') == homeGoals ? asIs : Build(true);
    }

    private static JsonElement? Value(List<StatisticItem> stats, string type) =>
        stats.FirstOrDefault(s => string.Equals(s.Type, type, StringComparison.OrdinalIgnoreCase))?.Value;

    private static int? Int(List<StatisticItem> stats, string type) => Value(stats, type) switch
    {
        { ValueKind: JsonValueKind.Number } v when v.TryGetInt32(out var i) => i,
        { ValueKind: JsonValueKind.String } v when int.TryParse(v.GetString()?.TrimEnd('%'), out var i) => i,
        _ => null,
    };

    private static decimal? Dec(List<StatisticItem> stats, string type) => Value(stats, type) switch
    {
        { ValueKind: JsonValueKind.Number } v when v.TryGetDecimal(out var d) => d,
        { ValueKind: JsonValueKind.String } v when decimal.TryParse(v.GetString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d) => d,
        _ => null,
    };

    private static string? Text(List<StatisticItem> stats, string type) => Value(stats, type) switch
    {
        { ValueKind: JsonValueKind.String } v => v.GetString(),
        { ValueKind: JsonValueKind.Number } v => v.GetRawText(),
        _ => null,
    };

    /// <summary>"45%" → 45.</summary>
    public static int? Percent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return decimal.TryParse(text.Trim().TrimEnd('%'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? (int)Math.Round(value, MidpointRounding.AwayFromZero)
            : null;
    }

    private static string? Truncate(string? text, int max) =>
        text is null ? null : text.Length <= max ? text : text[..max];
}
