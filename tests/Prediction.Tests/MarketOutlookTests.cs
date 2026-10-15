using Prediction.Core.Engine;
using Prediction.Core.Entities;
using static Prediction.Tests.TestData;

namespace Prediction.Tests;

public class MarketOutlookTests
{
    private static MatchContext Ctx(MarketMovement? ah = null, MarketMovement? ou = null, ApiAdvice? advice = null) => new()
    {
        Fixture = Match(1, 0, 33, 45),
        Home = Team(33, 3),
        Away = Team(45, 15),
        Handicap = ah,
        Totals = ou,
        Advice = advice,
    };

    private static MarketOutlook Outlook(MatchContext ctx, Market market, params RulePick[] picks) =>
        MatchAdvisor.Outlook(ctx, MatchAdvisor.Advise(ctx, picks, "MU", "Everton"), market, "MU", "Everton");

    [Fact]
    public void Rule_pick_is_shown_for_its_market_only()
    {
        var ctx = Ctx(Movement(Market.AsianHandicap, (-1m, 1.9m, 1.9m)), Movement(Market.OverUnder, (2.5m, 1.9m, 1.9m)));
        var pick = new RulePick(new Rule { Name = "MU" }, new Pick(PickSide.Home, -1m, 1.9m, PickSide.Home, 3, 15), RuleRecord.Empty);

        Assert.Equal(PickSide.Home, Outlook(ctx, Market.AsianHandicap, pick).RulePick?.Side);
        Assert.Null(Outlook(ctx, Market.OverUnder, pick).RulePick);
    }

    [Fact]
    public void Strong_trend_when_movement_and_api_agree()
    {
        var ctx = Ctx(
            Movement(Market.AsianHandicap, (-0.75m, 1.95m, 1.95m), (-1m, 1.9m, 2m)),
            advice: new ApiAdvice { WinnerTeamId = 33, PercentHome = 60 });

        var outlook = Outlook(ctx, Market.AsianHandicap);

        Assert.Null(outlook.RulePick);
        Assert.Equal(TrendStrength.Strong, outlook.Strength);
        Assert.Equal(PickSide.Home, outlook.TrendSide);
        Assert.Equal(-1m, outlook.TrendLine);
        Assert.Equal(1.9m, outlook.TrendOdds);
        Assert.Equal(2, outlook.TrendFor.Count);
    }

    [Fact]
    public void Totals_trend_from_api_only_is_weak()
    {
        var ctx = Ctx(ou: Movement(Market.OverUnder, (2.5m, 1.9m, 1.9m)), advice: new ApiAdvice { UnderOver = "-3.5" });

        var outlook = Outlook(ctx, Market.OverUnder);

        Assert.Equal(TrendStrength.Weak, outlook.Strength);
        Assert.Equal(PickSide.Under, outlook.TrendSide);
        Assert.Equal(2.5m, outlook.TrendLine);
    }

    [Fact]
    public void Mixed_signals_give_no_trend_side()
    {
        var ctx = Ctx(
            Movement(Market.AsianHandicap, (-0.75m, 1.95m, 1.95m), (-1m, 1.9m, 2m)),
            advice: new ApiAdvice { WinnerTeamId = 45 });

        var outlook = Outlook(ctx, Market.AsianHandicap);

        Assert.Equal(TrendStrength.Mixed, outlook.Strength);
        Assert.Null(outlook.TrendSide);
        Assert.NotEmpty(outlook.TrendFor);
        Assert.NotEmpty(outlook.TrendAgainst);
    }

    [Fact]
    public void Api_winner_outside_the_match_is_ignored()
    {
        var outlook = Outlook(Ctx(Movement(Market.AsianHandicap, (-1m, 1.9m, 1.9m)), advice: new ApiAdvice { WinnerTeamId = 999 }), Market.AsianHandicap);

        Assert.Equal(TrendStrength.None, outlook.Strength);
        Assert.Empty(outlook.TrendAgainst);
    }

    [Fact]
    public void No_signals()
    {
        var outlook = Outlook(Ctx(Movement(Market.AsianHandicap, (-1m, 1.9m, 1.9m))), Market.AsianHandicap);

        Assert.Equal(TrendStrength.None, outlook.Strength);
        Assert.Null(outlook.TrendSide);
        Assert.NotNull(outlook.Movement);
    }

    [Theory]
    [InlineData(1, 0, BetOutcome.HalfWin)]
    [InlineData(0, 0, BetOutcome.Loss)]
    [InlineData(null, null, null)]
    public void Provisional_result_at_current_score(int? hg, int? ag, BetOutcome? expected)
    {
        Assert.Equal(expected, Settlement.AtScore(PickSide.Home, -0.75m, 1.9m, hg, ag)?.Outcome);
    }

    [Theory]
    [InlineData("1H", true)]
    [InlineData("HT", true)]
    [InlineData("2H", true)]
    [InlineData("NS", false)]
    [InlineData("FT", false)]
    public void Live_statuses(string status, bool live)
    {
        Assert.Equal(live, FixtureStatus.IsLive(status));
    }
}
