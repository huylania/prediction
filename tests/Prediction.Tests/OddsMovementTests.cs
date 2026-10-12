using Prediction.Core.Engine;
using Prediction.Core.Entities;
using static Prediction.Tests.TestData;

namespace Prediction.Tests;

public class OddsMovementTests
{
    [Fact]
    public void Line_moving_deeper_for_home_leans_to_home()
    {
        var m = Movement(Market.AsianHandicap, (-0.75m, 1.95m, 1.95m), (-1m, 1.88m, 2.02m));

        Assert.Equal(-0.25m, m.LineDelta);
        Assert.Equal(Lean.Toward, m.LeanFor(PickSide.Home));
        Assert.Equal(Lean.Against, m.LeanFor(PickSide.Away));
    }

    [Fact]
    public void Price_drop_with_unchanged_line_leans_to_that_side()
    {
        var m = Movement(Market.AsianHandicap, (0.5m, 1.90m, 2.00m), (0.5m, 2.05m, 1.84m));

        Assert.Equal(Lean.Toward, m.LeanFor(PickSide.Away));
        Assert.Equal(Lean.Against, m.LeanFor(PickSide.Home));
        Assert.Equal(Lean.Neutral, m.LeanFor(PickSide.Away, minPriceDrop: 0.2m));
    }

    [Fact]
    public void Small_line_move_below_threshold_is_neutral()
    {
        var m = Movement(Market.AsianHandicap, (-0.5m, 1.9m, 1.9m), (-0.75m, 1.9m, 1.9m));

        Assert.Equal(Lean.Neutral, m.LeanFor(PickSide.Home, minLineMove: 0.5m));
    }

    [Fact]
    public void Totals_line_up_leans_over()
    {
        var m = Movement(Market.OverUnder, (2.5m, 1.9m, 1.9m), (2.75m, 1.95m, 1.85m));

        Assert.Equal(Lean.Toward, m.LeanFor(PickSide.Over));
        Assert.Equal(Lean.Against, m.LeanFor(PickSide.Under));
    }

    [Fact]
    public void Build_respects_cutoff_and_order()
    {
        var snapshots = new[]
        {
            Quote(1, Market.AsianHandicap, -0.5m, 1.9m, 1.9m, Start.AddHours(2), 2),
            Quote(1, Market.AsianHandicap, -0.25m, 1.9m, 1.9m, Start, 1),
            Quote(1, Market.AsianHandicap, -1m, 1.9m, 1.9m, Start.AddHours(5), 3),
            Quote(1, Market.OverUnder, 2.5m, 1.9m, 1.9m, Start, 4),
        };

        var m = MarketMovement.Build(snapshots, Market.AsianHandicap, Start.AddHours(3))!;

        Assert.Equal(2, m.Points.Count);
        Assert.Equal(-0.25m, m.Open.Line);
        Assert.Equal(-0.5m, m.Current.Line);
        Assert.Null(MarketMovement.Build(snapshots, Market.OverUnder, Start.AddHours(-1)));
    }

    [Fact]
    public void Describe_mentions_favourite_and_direction()
    {
        var m = Movement(Market.AsianHandicap, (-0.75m, 1.95m, 1.95m), (-1m, 1.88m, 2.02m));

        var text = m.Describe("MU", "Everton");

        Assert.Contains("MU chấp 3/4 trái", text);
        Assert.Contains("MU chấp 1 trái", text);
        Assert.Contains("tiền đổ về MU", text);
    }

    [Theory]
    [InlineData(0, "đồng banh")]
    [InlineData(0.25, "1/4 trái")]
    [InlineData(-0.75, "3/4 trái")]
    [InlineData(1.25, "1 1/4 trái")]
    [InlineData(2, "2 trái")]
    public void Handicap_text(decimal line, string expected)
    {
        Assert.Equal(expected, LineText.Handicap(line));
    }
}
