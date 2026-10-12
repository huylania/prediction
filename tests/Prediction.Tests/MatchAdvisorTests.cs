using Prediction.Core.Engine;
using Prediction.Core.Entities;
using static Prediction.Tests.TestData;

namespace Prediction.Tests;

public class MatchAdvisorTests
{
    private static MatchContext Ctx(MarketMovement ah, ApiAdvice? advice = null) => new()
    {
        Fixture = Match(1, 0, 33, 45),
        Home = Team(33, 3),
        Away = Team(45, 15),
        Handicap = ah,
        Advice = advice,
    };

    private static RulePick HomePick(string name, RuleRecord? record = null) =>
        new(new Rule { Name = name }, new Pick(PickSide.Home, -1m, 1.9m, PickSide.Home, 3, 15), record ?? RuleRecord.Empty);

    [Fact]
    public void All_signals_agree()
    {
        var ctx = Ctx(Movement(Market.AsianHandicap, (-0.75m, 1.95m, 1.95m), (-1m, 1.9m, 2m)), new ApiAdvice { WinnerTeamId = 33, PercentHome = 60 });

        var advice = MatchAdvisor.Advise(ctx, [HomePick("MU")], "MU", "Everton");

        var pick = Assert.Single(advice.Picks);
        Assert.Equal(3, pick.For.Count);
        Assert.Empty(pick.Against);
        Assert.Equal("3/3 tín hiệu ủng hộ", pick.Consensus);
        Assert.Equal("MU -1 @1.90", pick.Label);
        Assert.NotNull(advice.HandicapMovement);
        Assert.NotNull(advice.ApiSummary);
    }

    [Fact]
    public void Conflicts_are_reported()
    {
        var ctx = Ctx(Movement(Market.AsianHandicap, (-1m, 1.9m, 1.9m), (-0.75m, 1.95m, 1.95m)), new ApiAdvice { WinnerTeamId = 45 });
        var losing = new RuleRecord(20, 6, 0, 2, 0, 12, -5m);

        var advice = MatchAdvisor.Advise(ctx, [HomePick("MU", losing)], "MU", "Everton");

        var pick = Assert.Single(advice.Picks);
        Assert.True(pick.HasConflict);
        Assert.Contains(pick.Against, s => s.Contains("đang lỗ"));
        Assert.Contains(pick.Against, s => s.Contains("nghiêng về Everton"));
        Assert.Contains(pick.Against, s => s.Contains("API dự đoán Everton thắng"));
    }

    [Fact]
    public void Opposite_rules_count_against_each_other()
    {
        var ctx = Ctx(Movement(Market.AsianHandicap, (-1m, 1.9m, 1.9m)));
        var awayPick = new RulePick(new Rule { Name = "Theo khách" }, new Pick(PickSide.Away, 1m, 1.9m, PickSide.Away, 15, 3), RuleRecord.Empty);

        var advice = MatchAdvisor.Advise(ctx, [HomePick("MU"), awayPick], "MU", "Everton");

        Assert.Equal(2, advice.Picks.Count);
        Assert.All(advice.Picks, p => Assert.Contains(p.Against, s => s.Contains("cửa ngược lại")));
    }
}
