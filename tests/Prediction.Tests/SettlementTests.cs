using Prediction.Core.Engine;
using Prediction.Core.Entities;

namespace Prediction.Tests;

public class SettlementTests
{
    [Theory]
    // Đội nhà chấp 1/4 (−0.25)
    [InlineData(-0.25, 1, 0, BetOutcome.Win)]
    [InlineData(-0.25, 1, 1, BetOutcome.HalfLoss)]
    [InlineData(-0.25, 0, 1, BetOutcome.Loss)]
    // Chấp nửa trái (−0.5)
    [InlineData(-0.5, 1, 0, BetOutcome.Win)]
    [InlineData(-0.5, 2, 2, BetOutcome.Loss)]
    // Chấp 3/4 (−0.75)
    [InlineData(-0.75, 2, 0, BetOutcome.Win)]
    [InlineData(-0.75, 1, 0, BetOutcome.HalfWin)]
    [InlineData(-0.75, 0, 0, BetOutcome.Loss)]
    // Chấp 1 trái
    [InlineData(-1, 2, 0, BetOutcome.Win)]
    [InlineData(-1, 1, 0, BetOutcome.Push)]
    [InlineData(-1, 1, 1, BetOutcome.Loss)]
    // Chấp 1 1/4
    [InlineData(-1.25, 1, 0, BetOutcome.HalfLoss)]
    [InlineData(-1.25, 2, 0, BetOutcome.Win)]
    // Được chấp 1/4 (+0.25)
    [InlineData(0.25, 0, 0, BetOutcome.HalfWin)]
    [InlineData(0.25, 0, 1, BetOutcome.Loss)]
    // Đồng banh
    [InlineData(0, 1, 1, BetOutcome.Push)]
    public void Handicap_home(decimal line, int hg, int ag, BetOutcome expected)
    {
        Assert.Equal(expected, Settlement.Settle(PickSide.Home, line, 1.9m, hg, ag).Outcome);
    }

    [Fact]
    public void Handicap_away_uses_away_margin()
    {
        // Đội khách chấp 3/4, thắng 2-1 → thắng nửa.
        Assert.Equal(BetOutcome.HalfWin, Settlement.Settle(PickSide.Away, -0.75m, 2m, 1, 2).Outcome);
        // Đội khách được chấp 1/2, hòa → thắng.
        Assert.Equal(BetOutcome.Win, Settlement.Settle(PickSide.Away, 0.5m, 2m, 1, 1).Outcome);
    }

    [Theory]
    [InlineData(2.5, 3, BetOutcome.Win, BetOutcome.Loss)]
    [InlineData(2.5, 2, BetOutcome.Loss, BetOutcome.Win)]
    [InlineData(2.25, 2, BetOutcome.HalfLoss, BetOutcome.HalfWin)]
    [InlineData(2.25, 3, BetOutcome.Win, BetOutcome.Loss)]
    [InlineData(2.75, 3, BetOutcome.HalfWin, BetOutcome.HalfLoss)]
    [InlineData(2.75, 2, BetOutcome.Loss, BetOutcome.Win)]
    [InlineData(3, 3, BetOutcome.Push, BetOutcome.Push)]
    public void Totals(decimal line, int goals, BetOutcome over, BetOutcome under)
    {
        Assert.Equal(over, Settlement.Settle(PickSide.Over, line, 1.9m, goals, 0).Outcome);
        Assert.Equal(under, Settlement.Settle(PickSide.Under, line, 1.9m, 0, goals).Outcome);
    }

    [Fact]
    public void Profit_per_unit()
    {
        Assert.Equal(0.9m, Settlement.Settle(PickSide.Home, -0.5m, 1.9m, 1, 0).Profit);
        Assert.Equal(0.45m, Settlement.Settle(PickSide.Home, -0.75m, 1.9m, 1, 0).Profit);
        Assert.Equal(0m, Settlement.Settle(PickSide.Home, -1m, 1.9m, 1, 0).Profit);
        Assert.Equal(-0.5m, Settlement.Settle(PickSide.Home, -0.25m, 1.9m, 0, 0).Profit);
        Assert.Equal(-1m, Settlement.Settle(PickSide.Home, -0.5m, 1.9m, 0, 0).Profit);
    }

    [Fact]
    public void Rejects_lines_that_are_not_quarters()
    {
        Assert.Throws<ArgumentException>(() => Settlement.SplitLine(0.3m));
    }
}
