using Prediction.Core.Entities;

namespace Prediction.Core.Engine.Analysis;

/// <summary>Một mức kèo của nhà cái chuẩn (Pinnacle): Line theo đội nhà (chấp) hoặc tổng (tài xỉu); PriceA = đội nhà / Tài.</summary>
public sealed record SharpQuote(Market Market, decimal Line, decimal PriceA, decimal PriceB);

/// <summary>Kèo Pinnacle của một thị trường: mọi mức đang mở, mức chính hiện tại và mức chính lúc website thấy lần đầu.</summary>
public sealed class SharpMarket
{
    public required Market Market { get; init; }

    public IReadOnlyList<SharpQuote> Ladder { get; init; } = [];

    public SharpQuote? Main { get; init; }

    public SharpQuote? Open { get; init; }

    public DateTime? UpdatedUtc { get; init; }

    public SharpQuote? At(decimal line) => Ladder.FirstOrDefault(q => q.Line == line);
}

/// <summary>
/// So giá một cửa ở Bet365 với Pinnacle. Mức nhìn từ cửa được chọn. <see cref="Edge"/> = odds Bet365 × xác suất hợp lý (Pinnacle bỏ lời) − 1:
/// dương là Bet365 trả cao hơn giá chuẩn.
/// </summary>
public sealed record SharpComparison(decimal Line, decimal? SharpOdds, decimal? FairOdds, double? Edge, decimal? MainLine, decimal? OpenMainLine)
{
    /// <summary>Mức chính Pinnacle đã dịch về phía cửa này kể từ lúc mở (true), ngược (false), đứng (null).</summary>
    public bool? MovedToward(PickSide side) => MainLine is { } cur && OpenMainLine is { } open && cur != open
        ? side switch
        {
            PickSide.Over => cur > open,
            PickSide.Under => cur < open,
            _ => cur < open, // chấp: mức càng âm thì cửa này càng được đánh giá mạnh
        }
        : null;
}

public static class SharpPricing
{
    private static bool IsHandicap(Market market) => market is Market.AsianHandicap or Market.CornersHandicap;

    /// <summary>So giá cửa <paramref name="side"/> mức <paramref name="lineForSide"/> (nhìn từ cửa đó) với kèo Pinnacle; null nếu không có kèo.</summary>
    public static SharpComparison? Compare(SharpMarket? market, PickSide side, decimal lineForSide, decimal odds)
    {
        if (market is null || market.Ladder.Count == 0)
        {
            return null;
        }

        var handicap = IsHandicap(market.Market);
        decimal? ForSide(SharpQuote? q) => q is null ? null : handicap && side == PickSide.Away ? -q.Line : q.Line;
        var quoteLine = handicap && side == PickSide.Away ? -lineForSide : lineForSide;
        var main = ForSide(market.Main);
        var open = ForSide(market.Open);
        if (market.At(quoteLine) is not { } q)
        {
            return new SharpComparison(lineForSide, null, null, null, main, open);
        }

        var sideA = side is PickSide.Home or PickSide.Over;
        var (mine, other) = sideA ? (q.PriceA, q.PriceB) : (q.PriceB, q.PriceA);
        if (PickProbability.NoVig(mine, other) is not { } share || share <= 0)
        {
            return new SharpComparison(lineForSide, mine, null, null, main, open);
        }

        return new SharpComparison(lineForSide, mine, Math.Round((decimal)(1 / share), 2), (double)odds * share - 1, main, open);
    }
}
