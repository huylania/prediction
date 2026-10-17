using System.Text.Json;
using Prediction.Core.Engine.Analysis;
using Prediction.Core.Entities;
using Prediction.Web.ApiFootball;
using Prediction.Web.Services;

namespace Prediction.Tests;

public class SharpOddsTests
{
    private const int Pinnacle = 4;

    /// <summary>odds?league=40&amp;bookmaker=4 thật (Championship, trận 1563179), rút gọn còn vài loại kèo.</summary>
    private static OddsItem Item()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "odds-pinnacle.json"));
        var envelope = JsonSerializer.Deserialize<ApiEnvelope>(json, ApiFootballClient.Json)!;
        return envelope.Response.Deserialize<List<OddsItem>>(ApiFootballClient.Json)!.Single();
    }

    private static SharpMarket Market(Market market, decimal? openLine = null)
    {
        var rows = OddsParser.Ladder(Item(), Pinnacle)
            .Where(x => x.Quote.Market == market)
            .Select(x => new SharpOddsQuote { FixtureId = 1, Market = market, Line = x.Quote.Line, PriceA = x.Quote.PriceA, PriceB = x.Quote.PriceB, IsMain = x.IsMain })
            .ToList();
        if (openLine is { } ol)
        {
            rows.Add(new SharpOddsQuote { FixtureId = 1, Market = market, Line = ol, PriceA = 1.9m, PriceB = 1.9m, IsOpening = true });
        }

        return AnalysisService.SharpMarkets(rows)[1].Single();
    }

    [Fact]
    public void Ladder_reads_every_pinnacle_line_and_marks_the_main_one()
    {
        var ladder = OddsParser.Ladder(Item(), Pinnacle);

        var ah = ladder.Where(x => x.Quote.Market == Prediction.Core.Entities.Market.AsianHandicap).ToList();
        Assert.Equal(10, ah.Count); // −1 … +1.25
        Assert.Equal(0m, Assert.Single(ah, x => x.IsMain).Quote.Line); // 2.05 / 1.86 gần nhau nhất
        var minusOne = Assert.Single(ah, x => x.Quote.Line == -1m).Quote;
        Assert.Equal((4.40m, 1.24m), (minusOne.PriceA, minusOne.PriceB));

        var ou = ladder.Where(x => x.Quote.Market == Prediction.Core.Entities.Market.OverUnder).ToList();
        Assert.Equal(2.75m, Assert.Single(ou, x => x.IsMain).Quote.Line);
        Assert.Contains(ou, x => x.Quote.Line == 3.0m && x.Quote.PriceA == 2.21m && x.Quote.PriceB == 1.72m);

        Assert.Equal(10.5m, Assert.Single(ladder, x => x.Quote.Market == Prediction.Core.Entities.Market.CornersOverUnder && x.IsMain).Quote.Line);

        // Không lấy nhà cái khác thay thế (khác OddsParser.Parse).
        Assert.Empty(OddsParser.Ladder(Item(), 8));
    }

    [Fact]
    public void Compare_prices_bet365_against_pinnacle_fair_odds_at_the_same_line()
    {
        var totals = Market(Prediction.Core.Entities.Market.OverUnder, openLine: 2.5m);

        // Tài 3.0: Pinnacle 2.21 / 1.72 → xác suất hợp lý Tài ≈ 43.8%, giá hợp lý ≈ 2.28.
        var fair = SharpPricing.Compare(totals, PickSide.Over, 3.0m, 2.40m)!;
        Assert.Equal(2.28m, fair.FairOdds);
        Assert.InRange(fair.Edge!.Value, 0.04, 0.06);
        Assert.Equal(2.75m, fair.MainLine);
        Assert.True(fair.MovedToward(PickSide.Over)); // mở 2.5 → 2.75: thị trường chuẩn nghiêng Tài

        var ind = LinePatternScorer.SharpPrice(fair, PickSide.Over, 3.0m, 2.40m);
        Assert.Equal(1.5, ind.Points); // +1 giá tốt, +0.5 Pinnacle dịch về Tài
        Assert.Contains("cao hơn", ind.Text);

        var poor = LinePatternScorer.SharpPrice(SharpPricing.Compare(totals, PickSide.Over, 3.0m, 2.10m), PickSide.Over, 3.0m, 2.10m);
        Assert.Equal(-0.5, poor.Points); // −1 giá kém, +0.5 dịch về Tài
        Assert.Contains("thấp hơn", poor.Text);
    }

    [Fact]
    public void Away_favourite_reads_the_mirrored_home_line()
    {
        var ah = Market(Prediction.Core.Entities.Market.AsianHandicap);

        // Khách chấp 0.75 = mức đội nhà +0.75: Pinnacle cửa khách 3.09.
        var c = SharpPricing.Compare(ah, PickSide.Away, -0.75m, 3.20m)!;
        Assert.Equal(3.09m, c.SharpOdds);
        Assert.Equal(0m, c.MainLine);
        Assert.Null(c.MovedToward(PickSide.Away)); // chưa có mức mở

        // Pinnacle không mở mức −1.5: so mức chính (0) → Pinnacle đánh giá cửa này yếu hơn hẳn.
        var missing = SharpPricing.Compare(ah, PickSide.Home, -1.5m, 2.0m)!;
        Assert.Null(missing.Edge);
        var ind = LinePatternScorer.SharpPrice(missing, PickSide.Home, -1.5m, 2.0m);
        Assert.Equal(-0.5, ind.Points);
        Assert.Contains("không mở mức", ind.Text);
    }

    [Fact]
    public void No_pinnacle_odds_is_neutral()
    {
        Assert.Null(SharpPricing.Compare(null, PickSide.Over, 3m, 2m));
        Assert.Equal(0, LinePatternScorer.SharpPrice(null, PickSide.Over, 3m, 2m).Points);
    }
}
