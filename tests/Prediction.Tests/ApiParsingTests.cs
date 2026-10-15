using System.Text.Json;
using Prediction.Core.Entities;
using Prediction.Web.ApiFootball;
using Prediction.Web.Services;

namespace Prediction.Tests;

public class ApiParsingTests
{
    private static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, ApiFootballClient.Json)!;

    private static List<BetValue> Values(params (string Value, string Odd)[] values) =>
        Parse<List<BetValue>>(JsonSerializer.Serialize(values.Select(v => new { value = v.Value, odd = v.Odd })));

    [Fact]
    public void Handicap_mirrored_notation_picks_balanced_line()
    {
        var quote = OddsParser.MainHandicap(Values(
            ("Home -0.5", "1.70"), ("Away +0.5", "2.15"),
            ("Home -0.75", "1.93"), ("Away +0.75", "1.90"),
            ("Home -1", "2.30"), ("Away +1", "1.62")));

        Assert.NotNull(quote);
        Assert.Equal(-0.75m, quote.Line);
        Assert.Equal(1.93m, quote.PriceA);
        Assert.Equal(1.90m, quote.PriceB);
    }

    [Fact]
    public void Handicap_same_number_notation()
    {
        var quote = OddsParser.MainHandicap(Values(
            ("Home -1", "2.10"), ("Away -1", "1.75"),
            ("Home -0.75", "1.88"), ("Away -0.75", "1.95")));

        Assert.NotNull(quote);
        Assert.Equal(-0.75m, quote.Line);
        Assert.Equal(1.88m, quote.PriceA);
        Assert.Equal(1.95m, quote.PriceB);
    }

    [Fact]
    public void Handicap_ambiguous_symmetric_lines_use_overround_check()
    {
        // Ghi kiểu đối xứng: ghép "cùng số" sẽ ra cặp phi lý (tổng xác suất ~0.73) nên bị loại.
        var quote = OddsParser.MainHandicap(Values(
            ("Home -1", "2.10"), ("Away +1", "1.75"),
            ("Home +1", "1.25"), ("Away -1", "3.90")));

        Assert.NotNull(quote);
        Assert.Equal(-1m, quote.Line);
        Assert.Equal(2.10m, quote.PriceA);
    }

    [Fact]
    public void Real_bet365_handicap_uses_same_number_notation()
    {
        // Dữ liệu thật từ /odds?league=39&season=2026&bookmaker=8, trận 1557417 (15/10/2026).
        var quote = OddsParser.MainHandicap(Values(
            ("Home -1.25", "2.00"), ("Away -1.25", "1.85"), ("Home -1", "1.65"), ("Away -1", "2.20"),
            ("Home -0.75", "1.50"), ("Away -0.75", "2.50"), ("Home -0.5", "1.40"), ("Away -0.5", "2.85"),
            ("Home -0.25", "1.27"), ("Away -0.25", "3.55"), ("Home +0", "1.14"), ("Away +0", "5.75"),
            ("Home +0.25", "1.12"), ("Away +0.25", "6.25"), ("Home +0.5", "1.10"), ("Away +0.5", "6.80"),
            ("Home -2", "3.30"), ("Away -2", "1.32"), ("Home -1.75", "2.50"), ("Away -1.75", "1.50"),
            ("Home -1.5", "2.15"), ("Away -1.5", "1.68"), ("Home -2.5", "3.90"), ("Away -2.5", "1.24"),
            ("Home -2.25", "3.55"), ("Away -2.25", "1.27"), ("Home -2.75", "5.00"), ("Away -2.75", "1.17")));

        Assert.NotNull(quote);
        Assert.Equal(-1.25m, quote.Line);
        Assert.Equal(2.00m, quote.PriceA);
        Assert.Equal(1.85m, quote.PriceB);
    }

    [Fact]
    public void Real_bet365_handicap_level_ball()
    {
        // Trận 1557418: đội khách được đánh giá cao hơn một chút, mức chính đồng banh.
        var quote = OddsParser.MainHandicap(Values(
            ("Home -1.25", "5.00"), ("Away -1.25", "1.17"), ("Home -1", "4.40"), ("Away -1", "1.20"),
            ("Home -0.75", "3.30"), ("Away -0.75", "1.32"), ("Home -0.5", "2.68"), ("Away -0.5", "1.45"),
            ("Home -0.25", "2.35"), ("Away -0.25", "1.58"), ("Home +0", "1.95"), ("Away +0", "1.90"),
            ("Home +0.25", "1.72"), ("Away +0.25", "2.08"), ("Home +0.5", "1.55"), ("Away +0.5", "2.38"),
            ("Home +0.75", "1.40"), ("Away +0.75", "2.85"), ("Home +1", "1.26"), ("Away +1", "3.70")));

        Assert.NotNull(quote);
        Assert.Equal(0m, quote.Line);
        Assert.Equal(1.95m, quote.PriceA);
        Assert.Equal(1.90m, quote.PriceB);
    }

    [Fact]
    public void Same_number_wins_ties()
    {
        // Mỗi kiểu ghép cho đúng 1 cặp hợp lệ: "cùng số" (−0.5: 1.90/1.95) và đối xứng (−0.5: 1.90/2.10) → theo kiểu của API thật.
        var quote = OddsParser.MainHandicap(Values(("Home -0.5", "1.90"), ("Away -0.5", "1.95"), ("Away +0.5", "2.10")));

        Assert.NotNull(quote);
        Assert.Equal(-0.5m, quote.Line);
        Assert.Equal(1.95m, quote.PriceB);
    }

    [Fact]
    public void Real_bet365_totals()
    {
        var quote = OddsParser.MainTotals(Values(
            ("Over 1.5", "1.25"), ("Under 1.5", "4.00"), ("Over 2.5", "1.80"), ("Under 2.5", "2.00"),
            ("Over 3.5", "3.00"), ("Under 3.5", "1.40"), ("Over 0.5", "1.04"), ("Under 0.5", "13.00"),
            ("Over 4.5", "5.50"), ("Under 4.5", "1.14")));

        Assert.NotNull(quote);
        Assert.Equal(2.5m, quote.Line);
        Assert.Equal(1.80m, quote.PriceA);
        Assert.Equal(2.00m, quote.PriceB);
    }

    [Fact]
    public void Totals_main_line()
    {
        var quote = OddsParser.MainTotals(Values(
            ("Over 1.5", "1.25"), ("Under 1.5", "3.75"),
            ("Over 2.5", "1.95"), ("Under 2.5", "1.85"),
            ("Over 3.5", "3.40"), ("Under 3.5", "1.30"),
            ("Over 2.3", "1.90"), ("Under 2.3", "1.90")));

        Assert.NotNull(quote);
        Assert.Equal(2.5m, quote.Line);
        Assert.Equal(1.95m, quote.PriceA);
    }

    [Fact]
    public void Parses_odds_response_for_configured_bookmaker()
    {
        const string json = """
        {
          "get": "odds", "parameters": {"fixture": "1"}, "errors": [], "results": 1,
          "paging": {"current": 1, "total": 1},
          "response": [{
            "league": {"id": 39, "season": 2026},
            "fixture": {"id": 1001, "timezone": "UTC", "date": "2026-10-18T14:00:00+00:00"},
            "update": "2026-10-17T09:00:00+00:00",
            "bookmakers": [
              {"id": 6, "name": "Bwin", "bets": [{"id": 4, "name": "Asian Handicap", "values": [{"value": "Home -2", "odd": "1.90"}, {"value": "Away +2", "odd": "1.90"}]}]},
              {"id": 8, "name": "Bet365", "bets": [
                {"id": 4, "name": "Asian Handicap", "values": [{"value": "Home -1", "odd": "1.95"}, {"value": "Away +1", "odd": "1.90"}]},
                {"id": 19, "name": "Asian Handicap First Half", "values": [{"value": "Home -0.5", "odd": "1.95"}, {"value": "Away +0.5", "odd": "1.90"}]},
                {"id": 5, "name": "Goals Over/Under", "values": [{"value": "Over 2.75", "odd": "1.92"}, {"value": "Under 2.75", "odd": "1.92"}]}
              ]}
            ]
          }]
        }
        """;

        var envelope = Parse<ApiEnvelope>(json);
        var item = envelope.Response.Deserialize<List<OddsItem>>(ApiFootballClient.Json)!.Single();
        var quotes = OddsParser.Parse(item, 8);

        Assert.Null(ApiFootballClient.ErrorText(envelope.Errors));
        Assert.Equal(1001, item.Fixture.Id);
        Assert.Equal(new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc), item.Update!.Value.UtcDateTime);
        Assert.Equal(2, quotes.Count);
        Assert.Contains(quotes, q => q.Market == Market.AsianHandicap && q.Line == -1m && q.PriceA == 1.95m);
        Assert.Contains(quotes, q => q.Market == Market.OverUnder && q.Line == 2.75m);
    }

    [Fact]
    public void Error_object_is_reported()
    {
        var envelope = Parse<ApiEnvelope>("""{"errors": {"token": "Error/Missing application key."}, "results": 0, "response": []}""");

        Assert.Equal("token: Error/Missing application key.", ApiFootballClient.ErrorText(envelope.Errors));
    }

    [Fact]
    public void Maps_fixture_using_ninety_minute_score()
    {
        const string json = """
        {
          "fixture": {"id": 2002, "date": "2027-03-10T20:00:00+00:00", "status": {"long": "Match Finished", "short": "AET", "elapsed": 120}},
          "league": {"id": 2, "name": "UEFA Champions League", "season": 2026, "round": "Round of 16"},
          "teams": {"home": {"id": 33, "name": "Manchester United", "logo": "x"}, "away": {"id": 50, "name": "Manchester City", "logo": "y"}},
          "goals": {"home": 2, "away": 1},
          "score": {"halftime": {"home": 0, "away": 0}, "fulltime": {"home": 1, "away": 1}, "extratime": {"home": 1, "away": 0}, "penalty": {"home": null, "away": null}}
        }
        """;

        var fixture = new Fixture();
        ApiMapper.Apply(fixture, Parse<FixtureItem>(json), "Cup", DateTime.UtcNow);

        Assert.Equal(2002, fixture.Id);
        Assert.Equal(1, fixture.HomeGoals);
        Assert.Equal(1, fixture.AwayGoals);
        Assert.True(fixture.IsFinished);
        Assert.False(fixture.HasTable);
        Assert.Equal(new DateTime(2027, 3, 10, 20, 0, 0, DateTimeKind.Utc), fixture.KickoffUtc);
    }

    [Fact]
    public void Maps_live_fixture_with_elapsed_minute()
    {
        const string json = """
        {
          "fixture": {"id": 3003, "date": "2026-10-15T12:00:00+00:00", "status": {"long": "Second Half", "short": "2H", "elapsed": 67}},
          "league": {"id": 39, "season": 2026, "round": "Regular Season - 8"},
          "teams": {"home": {"id": 33, "name": "Manchester United"}, "away": {"id": 45, "name": "Everton"}},
          "goals": {"home": 1, "away": 0},
          "score": {"halftime": {"home": 1, "away": 0}, "fulltime": {"home": null, "away": null}}
        }
        """;

        var fixture = new Fixture();
        ApiMapper.Apply(fixture, Parse<FixtureItem>(json), "League", DateTime.UtcNow);

        Assert.True(fixture.IsLive);
        Assert.Equal(67, fixture.Elapsed);
        Assert.Equal(1, fixture.HomeGoals);
        Assert.Equal(0, fixture.AwayGoals);
        Assert.False(fixture.IsFinished);
    }

    [Theory]
    [InlineData("League", "Regular Season - 12", true)]
    [InlineData("Cup", "League Stage - 3", true)]
    [InlineData("Cup", "Round of 16", false)]
    [InlineData("Cup", "Group Stage - 2", false)]
    [InlineData("League", "Relegation Round", false)]
    [InlineData("League", "Matchday 4", true)]
    public void Round_classification(string type, string round, bool expected)
    {
        Assert.Equal(expected, ApiMapper.HasTable(type, round));
    }

    [Fact]
    public void Maps_prediction()
    {
        const string json = """
        {
          "predictions": {
            "winner": {"id": 33, "name": "Manchester United", "comment": "Win or draw"},
            "win_or_draw": true, "under_over": "-3.5",
            "goals": {"home": "-2.5", "away": "-1.5"},
            "advice": "Double chance : Manchester United or draw",
            "percent": {"home": "45%", "draw": "45%", "away": "10%"}
          },
          "comparison": {"form": {"home": "60%", "away": "40%"}, "att": {"home": "55%", "away": "45%"}, "def": {"home": "50%", "away": "50%"}, "h2h": {"home": "70%", "away": "30%"}, "total": {"home": "58.5%", "away": "41.5%"}}
        }
        """;

        var advice = new ApiAdvice();
        ApiMapper.Apply(advice, Parse<PredictionItem>(json), DateTime.UtcNow);

        Assert.Equal(33, advice.WinnerTeamId);
        Assert.Equal(PickSide.Under, advice.UnderOverSide);
        Assert.Equal(45, advice.PercentHome);
        Assert.Equal(10, advice.PercentAway);
        Assert.Equal(70, advice.H2HHome);
        Assert.Equal(59, advice.TotalHome);
        Assert.Equal("Double chance : Manchester United or draw", advice.Advice);
    }

    [Fact]
    public void Odds_store_writes_only_changes()
    {
        var now = DateTime.UtcNow;
        var quote = new ParsedQuote(Market.AsianHandicap, -0.75m, 1.9m, 1.95m);

        var first = OddsStore.NextSnapshot(null, 1, quote, 8, null, now, OddsSource.Api);
        Assert.NotNull(first);
        Assert.Null(OddsStore.NextSnapshot(first, 1, quote, 8, null, now.AddHours(1), OddsSource.Api));
        Assert.NotNull(OddsStore.NextSnapshot(first, 1, quote with { PriceA = 1.88m }, 8, null, now.AddHours(1), OddsSource.Api));
        Assert.NotNull(OddsStore.NextSnapshot(first, 1, quote with { Line = -1m }, 8, null, now.AddHours(1), OddsSource.Api));
    }
}
