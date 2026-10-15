using System.Text.Json;
using Prediction.Core.Entities;
using Prediction.Web.ApiFootball;
using Prediction.Web.Services;

namespace Prediction.Tests;

public class MatchStatsParsingTests
{
    private static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, ApiFootballClient.Json)!;

    private static List<BetValue> Values(params (string Value, string Odd)[] values) =>
        Parse<List<BetValue>>(JsonSerializer.Serialize(values.Select(v => new { value = v.Value, odd = v.Odd })));

    [Fact]
    public void Real_bet365_corner_and_card_markets()
    {
        // Dữ liệu thật từ /odds?league=39&season=2026&bookmaker=8, trận 1557417.
        const string json = """
        {
          "fixture": {"id": 1557417},
          "update": "2026-10-10T09:40:25+00:00",
          "bookmakers": [{"id": 8, "name": "Bet365", "bets": [
            {"id": 45, "name": "Corners Over Under", "values": [{"value": "Over 9.5", "odd": "1.83"}, {"value": "Under 9.5", "odd": "1.83"}]},
            {"id": 56, "name": "Corners Asian Handicap", "values": [{"value": "Home -3.5", "odd": "1.90"}, {"value": "Away -3.5", "odd": "1.90"}]},
            {"id": 80, "name": "Cards Over/Under", "values": [{"value": "Over 2.5", "odd": "1.57"}, {"value": "Under 2.5", "odd": "2.25"}, {"value": "Over 3.5", "odd": "2.20"}, {"value": "Under 3.5", "odd": "1.62"}]}
          ]}]
        }
        """;

        var quotes = OddsParser.Parse(Parse<OddsItem>(json), 8);

        var corners = Assert.Single(quotes, q => q.Market == Market.CornersOverUnder);
        Assert.Equal(9.5m, corners.Line);
        Assert.Equal(1.83m, corners.PriceA);

        var cornersHandicap = Assert.Single(quotes, q => q.Market == Market.CornersHandicap);
        Assert.Equal(-3.5m, cornersHandicap.Line);
        Assert.Equal(1.90m, cornersHandicap.PriceA);

        var cards = Assert.Single(quotes, q => q.Market == Market.CardsOverUnder);
        Assert.Equal(3.5m, cards.Line); // 2.20/1.62 cân hơn 1.57/2.25
        Assert.Equal(2.20m, cards.PriceA);
        Assert.Equal(1.62m, cards.PriceB);
    }

    [Fact]
    public void Corner_totals_with_whole_number_line()
    {
        var quote = OddsParser.MainTotals(Values(("Over 10", "1.90"), ("Under 10", "1.90"), ("Over 10.5", "2.10"), ("Under 10.5", "1.67")), Market.CornersOverUnder);

        Assert.NotNull(quote);
        Assert.Equal(Market.CornersOverUnder, quote.Market);
        Assert.Equal(10m, quote.Line);
    }

    // Trận thật 1557424 Liverpool 0-3 Man City (EPL 2026/27), rút gọn.
    private const string LiverpoolCity = """
    {
      "fixture": {"id": 1557424, "date": "2026-10-04T15:30:00+00:00", "status": {"short": "FT", "elapsed": 90}},
      "league": {"id": 39, "season": 2026, "round": "Regular Season - 7"},
      "teams": {"home": {"id": 40, "name": "Liverpool"}, "away": {"id": 50, "name": "Manchester City"}},
      "goals": {"home": 0, "away": 3},
      "score": {"halftime": {"home": 0, "away": 0}, "fulltime": {"home": 0, "away": 3}},
      "events": [
        {"time": {"elapsed": 65, "extra": null}, "team": {"id": 50}, "type": "Goal", "detail": "Normal Goal"},
        {"time": {"elapsed": 66, "extra": null}, "team": {"id": 40}, "type": "Card", "detail": "Yellow Card"},
        {"time": {"elapsed": 68, "extra": null}, "team": {"id": 50}, "type": "Goal", "detail": "Normal Goal"},
        {"time": {"elapsed": 78, "extra": null}, "team": {"id": 50}, "type": "Goal", "detail": "Normal Goal"}
      ],
      "statistics": [
        {"team": {"id": 40}, "statistics": [
          {"type": "Shots on Goal", "value": 6}, {"type": "Total Shots", "value": 13}, {"type": "Corner Kicks", "value": 5},
          {"type": "Ball Possession", "value": "35%"}, {"type": "Yellow Cards", "value": 4}, {"type": "Red Cards", "value": 1},
          {"type": "expected_goals", "value": "0.94"}]},
        {"team": {"id": 50}, "statistics": [
          {"type": "Shots on Goal", "value": 4}, {"type": "Total Shots", "value": 15}, {"type": "Corner Kicks", "value": 6},
          {"type": "Ball Possession", "value": "65%"}, {"type": "Yellow Cards", "value": 2}, {"type": "Red Cards", "value": null},
          {"type": "expected_goals", "value": "1.28"}]}
      ]
    }
    """;

    [Fact]
    public void Maps_real_fixture_statistics()
    {
        var stats = new FixtureStats { FixtureId = 1557424 };
        var ok = ApiMapper.ApplyStats(stats, Parse<FixtureItem>(LiverpoolCity), DateTime.UtcNow);

        Assert.True(ok);
        Assert.Equal(5, stats.HomeCorners);
        Assert.Equal(6, stats.AwayCorners);
        Assert.Equal(11, stats.TotalCorners);
        Assert.Equal(13, stats.HomeShots);
        Assert.Equal(4, stats.AwayShotsOnTarget);
        Assert.Equal(35, stats.HomePossession);
        Assert.Equal(0.94m, stats.HomeXg);
        Assert.Equal(1.28m, stats.AwayXg);
        Assert.Equal(1, stats.HomeRed);
        Assert.Equal(0, stats.AwayRed); // null trong bảng thống kê = 0
        Assert.Equal(7, stats.TotalCards);
        Assert.Equal(0, stats.HalftimeHome);
        Assert.Equal("65A,68A,78A", stats.GoalTimeline);
        Assert.Equal(65, stats.FirstGoalMinute);
        Assert.All(stats.Goals(), g => Assert.False(g.Home));
    }

    [Fact]
    public void Missing_statistics_keeps_numbers_empty()
    {
        const string json = """
        {
          "fixture": {"id": 9, "date": "2026-10-04T15:30:00+00:00", "status": {"short": "FT"}},
          "league": {"id": 340, "season": 2027},
          "teams": {"home": {"id": 1, "name": "A"}, "away": {"id": 2, "name": "B"}},
          "goals": {"home": 1, "away": 0},
          "score": {"halftime": {"home": 1, "away": 0}},
          "events": [{"time": {"elapsed": 45, "extra": 2}, "team": {"id": 1}, "type": "Goal", "detail": "Penalty"}],
          "statistics": []
        }
        """;

        var stats = new FixtureStats { FixtureId = 9 };
        var ok = ApiMapper.ApplyStats(stats, Parse<FixtureItem>(json), DateTime.UtcNow);

        Assert.False(ok);
        Assert.Null(stats.HomeCorners);
        Assert.Null(stats.TotalCorners);
        Assert.Equal("45+2H", stats.GoalTimeline);
        Assert.Equal(45, stats.FirstGoalMinute);
    }

    [Theory]
    [InlineData(1, "10A,30H")] // tỉ số 1-1: bàn phản lưới ghi theo đội của cầu thủ phản lưới → đảo sang đội khách
    [InlineData(2, "10H,30H")] // tỉ số 2-0: bàn phản lưới đã ghi cho đội được hưởng
    public void Own_goal_assignment_matches_score(int homeGoals, string expected)
    {
        var json = $$$"""
        {
          "fixture": {"id": 7, "date": "2026-10-04T15:30:00+00:00", "status": {"short": "FT"}},
          "league": {"id": 39, "season": 2026},
          "teams": {"home": {"id": 1, "name": "A"}, "away": {"id": 2, "name": "B"}},
          "goals": {"home": {{{homeGoals}}}, "away": {{{2 - homeGoals}}}},
          "events": [
            {"time": {"elapsed": 10}, "team": {"id": 1}, "type": "Goal", "detail": "Own Goal"},
            {"time": {"elapsed": 30}, "team": {"id": 1}, "type": "Goal", "detail": "Normal Goal"},
            {"time": {"elapsed": 50}, "team": {"id": 2}, "type": "Goal", "detail": "Missed Penalty"}
          ]
        }
        """;

        Assert.Equal(expected, ApiMapper.GoalTimeline(Parse<FixtureItem>(json)));
    }

    [Fact]
    public void Current_coach_is_open_career_entry_with_latest_start()
    {
        const string json = """
        [
          {"id": 1, "name": "Old Boss", "career": [{"team": {"id": 33}, "start": "2022-07-01", "end": "2025-11-10"}]},
          {"id": 2, "name": "New Boss", "career": [
            {"team": {"id": 99}, "start": "2020-01-01", "end": "2025-06-30"},
            {"team": {"id": 33}, "start": "2026-09-20", "end": null}]},
          {"id": 3, "name": "Stale Entry", "career": [
            {"team": {"id": null, "name": "Unknown club"}, "start": "2027-01-01", "end": null},
            {"team": {"id": 33}, "start": "2019-01-01", "end": null}]}
        ]
        """;

        var coach = DataSyncService.CurrentCoach(Parse<List<CoachItem>>(json), 33);

        Assert.NotNull(coach);
        Assert.Equal("New Boss", coach.Value.Name);
        Assert.Equal(new DateTime(2026, 9, 20), coach.Value.Start.Date);
        Assert.Null(DataSyncService.CurrentCoach(Parse<List<CoachItem>>(json), 77));
    }

    [Fact]
    public void League_coverage_and_calendar_season()
    {
        const string json = """
        {
          "league": {"id": 253, "name": "Major League Soccer", "type": "League"},
          "country": {"name": "USA"},
          "seasons": [{"year": 2026, "start": "2026-02-21", "end": "2026-11-08", "current": true,
            "coverage": {"fixtures": {"events": true, "statistics_fixtures": true}, "injuries": false, "predictions": true, "odds": true}}]
        }
        """;

        var league = new League { Id = 253 };
        DataSyncService.Apply(league, Parse<LeagueItem>(json));

        Assert.True(league.SeasonIsCalendarYear);
        Assert.False(league.SeasonYearIsEndYear);
        Assert.True(league.HasStatsCoverage);
        Assert.False(league.HasInjuriesCoverage);
        Assert.Equal("2026", league.SeasonLabel(2026));
    }
}
