using System.Text.Json;
using Prediction.Core.Entities;
using Prediction.Web.ApiFootball;

namespace Prediction.Tests;

public class LiveOddsTests
{
    private static List<LiveOddsItem> RealItems()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "odds-live.json"));
        var envelope = JsonSerializer.Deserialize<ApiEnvelope>(json, ApiFootballClient.Json)!;
        return envelope.Response.Deserialize<List<LiveOddsItem>>(ApiFootballClient.Json)!;
    }

    private static LiveGoalQuote Quote(List<LiveGoalQuote> quotes, Market market, bool firstHalf) =>
        Assert.Single(quotes, q => q.Market == market && q.FirstHalf == firstHalf);

    [Fact]
    public void Parses_real_live_handicap_and_totals()
    {
        var items = RealItems();

        // C2 châu Á 1630370, phút 12, 0-0: đội nhà được chấp 1.5 (Home +1.5 / Away -1.5 — kiểu đối xứng).
        var acl2 = LiveOddsParser.ParseGoals(items.Single(i => i.Fixture.Id == 1630370));
        Assert.Equal(4, acl2.Count);
        var ah = Quote(acl2, Market.AsianHandicap, false);
        Assert.Equal((1.5m, 1.825m, 1.975m), (ah.Line, ah.PriceA, ah.PriceB));
        Assert.Equal(12, ah.Minute);
        var ou = Quote(acl2, Market.OverUnder, false);
        Assert.Equal((3m, 1.925m, 1.875m), (ou.Line, ou.PriceA, ou.PriceB));
        var ah1 = Quote(acl2, Market.AsianHandicap, true);
        Assert.Equal((0.5m, 1.95m, 1.85m), (ah1.Line, ah1.PriceA, ah1.PriceB));
        var ou1 = Quote(acl2, Market.OverUnder, true);
        Assert.Equal((1.25m, 2.05m, 1.75m), (ou1.Line, ou1.PriceA, ou1.PriceB));

        // 1533790 đang 0-1 phút 12: tài xỉu 3.25 là tổng bàn cả trận (đã gồm bàn đã ghi); kèo chấp cũng theo tỉ số cả trận.
        var other = LiveOddsParser.ParseGoals(items.Single(i => i.Fixture.Id == 1533790));
        var otherAh = Quote(other, Market.AsianHandicap, false);
        Assert.Equal((-0.25m, 1.825m, 1.975m), (otherAh.Line, otherAh.PriceA, otherAh.PriceB));
        Assert.Equal((0, 1), (otherAh.HomeGoals, otherAh.AwayGoals)); // tỉ số lấy từ "teams" của odds/live
        Assert.Equal(3.25m, Quote(other, Market.OverUnder, false).Line);

        // Hiệp 2 phút 80: không còn kèo hiệp 1.
        var late = LiveOddsParser.ParseGoals(items.Single(i => i.Fixture.Id == 1640328));
        Assert.Equal(2, late.Count);
        Assert.Equal(-0.25m, Quote(late, Market.AsianHandicap, false).Line);
        Assert.Equal(0.5m, Quote(late, Market.OverUnder, false).Line);
    }

    [Fact]
    public void Corner_quotes_unchanged_by_goal_markets()
    {
        var items = RealItems();
        var acl2 = LiveOddsParser.Parse(items.Single(i => i.Fixture.Id == 1630370));
        Assert.Equal(9m, Assert.Single(acl2, q => q.Window == CornerWindow.SecondHalf).Line);
        Assert.Equal(3.5m, Assert.Single(acl2, q => q.Window == CornerWindow.FirstHalf).Line);
    }

    [Fact]
    public void Without_main_flag_picks_closest_odds_and_skips_suspended_and_same_number_style()
    {
        LiveBetValue V(string value, string handicap, string odd, bool suspended = false) =>
            new() { Value = value, Handicap = handicap, Odd = odd, Suspended = suspended };

        var item = new LiveOddsItem
        {
            Fixture = new LiveOddsFixture { Id = 1, Status = new StatusInfo { Elapsed = 55 } },
            Odds =
            [
                new LiveBet
                {
                    Name = "Asian Handicap",
                    Values =
                    [
                        // Kiểu cùng số (như kèo trước trận): "Home -0.5" / "Away -0.5" là một cặp.
                        V("Home", "-0.5", "1.95"), V("Away", "-0.5", "1.90"),
                        V("Home", "-0.75", "2.40"), V("Away", "-0.75", "1.58"),
                        V("Home", "-0.25", "1.70", suspended: true), V("Away", "-0.25", "2.15"),
                    ],
                },
                new LiveBet
                {
                    Name = "Over/Under Line",
                    Values = [V("Over", "2.25", "1.50", suspended: true), V("Under", "2.25", "2.55"), V("Over", "2.5", "2.30"), V("Under", "2.5", "1.62")],
                },
                new LiveBet
                {
                    // Chỉ dùng khi "Over/Under Line" không có cặp hợp lệ (ở đây có nên không dùng).
                    Name = "Match Goals",
                    Values = [V("Over", "2.5", "2.25"), V("Under", "2.5", "1.65"), V("Over", "1.5", "1.20"), V("Under", "1.5", "4.33")],
                },
            ],
        };

        var quotes = LiveOddsParser.ParseGoals(item);
        var ah = Quote(quotes, Market.AsianHandicap, false);
        Assert.Equal((-0.5m, 1.95m, 1.90m), (ah.Line, ah.PriceA, ah.PriceB));
        Assert.Equal(55, ah.Minute);
        var ou = Quote(quotes, Market.OverUnder, false);
        Assert.Equal((2.5m, 2.30m, 1.62m), (ou.Line, ou.PriceA, ou.PriceB));
        Assert.DoesNotContain(quotes, q => q.FirstHalf);
    }

    [Fact]
    public void Handicap_from_current_score()
    {
        // Gangwon (nhà) chấp 1.25 trước trận; nghỉ giữa hiệp đang thua 0-1, API báo Home +0.25 (theo tỉ số cả trận)
        // → tính từ tỉ số 0-1 (kiểu châu Á) Gangwon vẫn chấp 3/4.
        var ah = new LiveOddsSnapshot { Market = Market.AsianHandicap, Line = 0.25m, HomeGoals = 0, AwayGoals = 1 };
        Assert.Equal(-0.75m, ah.FromScoreLine);

        var level = new LiveOddsSnapshot { Market = Market.AsianHandicap, Line = 1m, HomeGoals = 1, AwayGoals = 1 };
        Assert.Equal(1m, level.FromScoreLine);

        // Tài xỉu giữ nguyên tổng bàn cả trận.
        var ou = new LiveOddsSnapshot { Market = Market.OverUnder, Line = 2.5m, HomeGoals = 0, AwayGoals = 1 };
        Assert.Equal(2.5m, ou.FromScoreLine);
    }
}
