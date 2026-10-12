using System.Globalization;
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
        target.UpdatedAtUtc = nowUtc;
    }

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
