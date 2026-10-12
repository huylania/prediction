using Prediction.Core.Engine;
using Prediction.Core.Entities;
using static Prediction.Tests.TestData;

namespace Prediction.Tests;

public class BacktestRunnerTests
{
    private static MatchContext Ctx(int id, int hg, int ag, MarketMovement? ah = null) => new()
    {
        Fixture = Match(id, id, 33, 45, hg, ag),
        Home = Team(33, 2),
        Away = Team(45, 12),
        Handicap = ah,
    };

    private static Rule Rule() => new()
    {
        Name = "MU",
        Market = Market.AsianHandicap,
        Selection = RuleSelection.Subject,
        SubjectTeamId = 33,
        MinRankGap = 1,
    };

    [Fact]
    public void Result_stats_without_odds()
    {
        var report = BacktestRunner.Run(Rule(), [Ctx(1, 2, 0), Ctx(2, 1, 0), Ctx(3, 1, 1), Ctx(4, 0, 1)]);

        Assert.Equal(4, report.ResultMatches);
        Assert.Equal(2, report.SideWins);
        Assert.Equal(1, report.SideDraws);
        Assert.Equal(1, report.SideLosses);
        Assert.Empty(report.Bets);

        var minusHalf = report.HandicapRows.Single(r => r.Line == -0.5m).Counts;
        Assert.Equal(2, minusHalf.Win);
        Assert.Equal(2, minusHalf.Loss);
        Assert.Equal(2m, minusHalf.BreakEvenOdds);

        var minusOne = report.HandicapRows.Single(r => r.Line == -1m).Counts;
        Assert.Equal(1, minusOne.Win);
        Assert.Equal(1, minusOne.Push);
        Assert.Equal(2, minusOne.Loss);
    }

    [Fact]
    public void Profit_with_closing_odds()
    {
        var contexts = new[]
        {
            Ctx(1, 2, 0, Movement(Market.AsianHandicap, (-1m, 2m, 1.8m))),     // thắng +1.0
            Ctx(2, 1, 0, Movement(Market.AsianHandicap, (-0.75m, 2m, 1.8m))),  // thắng nửa +0.5
            Ctx(3, 0, 0, Movement(Market.AsianHandicap, (-0.5m, 1.9m, 1.9m))), // thua −1
            Ctx(4, 0, 1, Movement(Market.AsianHandicap, (-0.25m, 1.9m, 1.9m))),// thua −1
            Ctx(5, 3, 0),                                                      // không có kèo
        };

        var report = BacktestRunner.Run(Rule(), contexts);

        Assert.Equal(4, report.Bets.Count);
        Assert.Equal(4, report.FixturesWithOdds);
        Assert.Equal(-0.5m, report.Profit);
        Assert.Equal(2, report.LongestLosingStreak);
        Assert.Equal(2m, report.MaxDrawdown);
        Assert.Equal(-0.125m, report.Roi);
    }

    [Fact]
    public void Odds_only_rule_has_no_result_stats()
    {
        var rule = new Rule { Name = "Cửa trên chấp 1/4", Market = Market.AsianHandicap, Selection = RuleSelection.Subject, AhLineMin = -0.25m, AhLineMax = -0.25m };

        var report = BacktestRunner.Run(rule, [Ctx(1, 1, 0, Movement(Market.AsianHandicap, (-0.25m, 1.9m, 1.9m)))]);

        Assert.True(report.ResultStatsUnavailable);
        Assert.Single(report.Bets);
    }
}
