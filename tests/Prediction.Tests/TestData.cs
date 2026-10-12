using Prediction.Core.Engine;
using Prediction.Core.Entities;

namespace Prediction.Tests;

internal static class TestData
{
    public static readonly DateTime Start = new(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc);

    public static Fixture Match(int id, int day, int home, int away, int? hg = null, int? ag = null, int league = 39, bool hasTable = true) => new()
    {
        Id = id,
        LeagueId = league,
        Season = 2026,
        KickoffUtc = Start.AddDays(day),
        HomeTeamId = home,
        AwayTeamId = away,
        HomeGoals = hg,
        AwayGoals = ag,
        Status = hg.HasValue ? FixtureStatus.FullTime : FixtureStatus.NotStarted,
        HasTable = hasTable,
    };

    public static OddsSnapshot Quote(int fixtureId, Market market, decimal line, decimal a, decimal b, DateTime at, long id = 0) => new()
    {
        Id = id,
        FixtureId = fixtureId,
        Market = market,
        Line = line,
        PriceA = a,
        PriceB = b,
        FetchedAtUtc = at,
        Source = OddsSource.Api,
    };

    public static TeamSnapshot Team(int id, int rank, int played = 10, int points = 15, string form = "WDLWD", decimal? avgGoals = 2.5m) => new()
    {
        TeamId = id,
        Rank = rank,
        Played = played,
        Points = points,
        Form = form,
        AvgTotalGoals = avgGoals,
    };

    public static MarketMovement Movement(Market market, params (decimal Line, decimal A, decimal B)[] points) => new()
    {
        Market = market,
        Points = points.Select((p, i) => new OddsPoint(Start.AddHours(i), p.Line, p.A, p.B, OddsSource.Api)).ToList(),
    };
}
