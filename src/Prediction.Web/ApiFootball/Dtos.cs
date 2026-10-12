using System.Text.Json;

namespace Prediction.Web.ApiFootball;

// DTO của API-Football v3. Tên thuộc tính khớp JSON qua JsonNamingPolicy.SnakeCaseLower.

public sealed class ApiEnvelope
{
    public JsonElement Errors { get; set; }
    public int Results { get; set; }
    public ApiPaging? Paging { get; set; }
    public JsonElement Response { get; set; }
}

public sealed class ApiPaging
{
    public int Current { get; set; }
    public int Total { get; set; }
}

// --- /fixtures ---

public sealed class FixtureItem
{
    public FixtureInfo Fixture { get; set; } = new();
    public LeagueRef League { get; set; } = new();
    public TeamsPair Teams { get; set; } = new();
    public GoalsPair Goals { get; set; } = new();
    public ScoreInfo? Score { get; set; }
}

public sealed class FixtureInfo
{
    public int Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public StatusInfo Status { get; set; } = new();
}

public sealed class StatusInfo
{
    public string? Long { get; set; }
    public string Short { get; set; } = "NS";
    public int? Elapsed { get; set; }
}

public sealed class LeagueRef
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Country { get; set; }
    public string? Logo { get; set; }
    public int Season { get; set; }
    public string? Round { get; set; }
}

public sealed class TeamsPair
{
    public TeamRef Home { get; set; } = new();
    public TeamRef Away { get; set; } = new();
}

public sealed class TeamRef
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Logo { get; set; }
}

public sealed class GoalsPair
{
    public int? Home { get; set; }
    public int? Away { get; set; }
}

public sealed class ScoreInfo
{
    public GoalsPair? Halftime { get; set; }
    public GoalsPair? Fulltime { get; set; }
    public GoalsPair? Extratime { get; set; }
    public GoalsPair? Penalty { get; set; }
}

// --- /leagues ---

public sealed class LeagueItem
{
    public LeagueInfo League { get; set; } = new();
    public CountryInfo? Country { get; set; }
    public List<SeasonInfo> Seasons { get; set; } = [];
}

public sealed class LeagueInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Type { get; set; }
    public string? Logo { get; set; }
}

public sealed class CountryInfo
{
    public string? Name { get; set; }
}

public sealed class SeasonInfo
{
    public int Year { get; set; }
    public bool Current { get; set; }
    public string? Start { get; set; }
    public string? End { get; set; }
}

// --- /odds ---

public sealed class OddsItem
{
    public OddsFixtureRef Fixture { get; set; } = new();
    public DateTimeOffset? Update { get; set; }
    public List<BookmakerInfo> Bookmakers { get; set; } = [];
}

public sealed class OddsFixtureRef
{
    public int Id { get; set; }
    public DateTimeOffset? Date { get; set; }
}

public sealed class BookmakerInfo
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public List<BetInfo> Bets { get; set; } = [];
}

public sealed class BetInfo
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public List<BetValue> Values { get; set; } = [];
}

public sealed class BetValue
{
    public JsonElement Value { get; set; }
    public JsonElement Odd { get; set; }
}

// --- /predictions ---

public sealed class PredictionItem
{
    public PredictionInfo Predictions { get; set; } = new();
    public ComparisonInfo? Comparison { get; set; }
}

public sealed class PredictionInfo
{
    public WinnerInfo? Winner { get; set; }
    public bool? WinOrDraw { get; set; }
    public string? UnderOver { get; set; }
    public HomeAwayText? Goals { get; set; }
    public string? Advice { get; set; }
    public PercentInfo? Percent { get; set; }
}

public sealed class WinnerInfo
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public string? Comment { get; set; }
}

public sealed class PercentInfo
{
    public string? Home { get; set; }
    public string? Draw { get; set; }
    public string? Away { get; set; }
}

public sealed class HomeAwayText
{
    public string? Home { get; set; }
    public string? Away { get; set; }
}

public sealed class ComparisonInfo
{
    public HomeAwayText? Form { get; set; }
    public HomeAwayText? Att { get; set; }
    public HomeAwayText? Def { get; set; }
    public HomeAwayText? H2h { get; set; }
    public HomeAwayText? Goals { get; set; }
    public HomeAwayText? Total { get; set; }
}

// --- /status ---

public sealed class StatusResponse
{
    public SubscriptionInfo? Subscription { get; set; }
    public RequestsInfo? Requests { get; set; }
}

public sealed class SubscriptionInfo
{
    public string? Plan { get; set; }
    public DateTimeOffset? End { get; set; }
    public bool Active { get; set; }
}

public sealed class RequestsInfo
{
    public int Current { get; set; }
    public int LimitDay { get; set; }
}
