using Prediction.Core.Entities;

namespace Prediction.Core.Engine;

public sealed record OddsPoint(DateTime AtUtc, decimal Line, decimal PriceA, decimal PriceB, OddsSource Source);

/// <summary>Thị trường đang nghiêng về phía nào so với một cửa.</summary>
public enum Lean
{
    Neutral = 0,
    Toward = 1,
    Against = 2,
}

/// <summary>Biến động của một loại kèo trong một trận: từ kèo mở đến kèo hiện tại (hoặc kèo đóng).</summary>
public sealed class MarketMovement
{
    public const decimal DefaultMinLineMove = 0.25m;
    public const decimal DefaultMinPriceDrop = 0.05m;

    public required Market Market { get; init; }

    /// <summary>Các lần thay đổi, cũ → mới.</summary>
    public required IReadOnlyList<OddsPoint> Points { get; init; }

    public OddsPoint Open => Points[0];

    public OddsPoint Current => Points[^1];

    public int Changes => Points.Count - 1;

    /// <summary>Kèo chấp: thay đổi mức chấp của đội nhà. Tài xỉu: thay đổi mức tổng bàn.</summary>
    public decimal LineDelta => Current.Line - Open.Line;

    /// <summary>Tiền kèo cửa A (nhà/tài) giảm bao nhiêu (dương = giảm = tiền đổ vào).</summary>
    public decimal PriceADrop => Open.PriceA - Current.PriceA;

    public decimal PriceBDrop => Open.PriceB - Current.PriceB;

    /// <summary>
    /// Dựng biến động từ các snapshot của MỘT trận. Chỉ lấy snapshot đến <paramref name="cutoffUtc"/>
    /// (vd giờ bóng lăn để có kèo đóng). Trả về null nếu chưa có kèo.
    /// </summary>
    public static MarketMovement? Build(IEnumerable<OddsSnapshot> snapshots, Market market, DateTime? cutoffUtc = null)
    {
        var points = snapshots
            .Where(s => s.Market == market && (cutoffUtc is null || s.FetchedAtUtc <= cutoffUtc))
            .OrderBy(s => s.FetchedAtUtc)
            .ThenBy(s => s.Id)
            .Select(s => new OddsPoint(s.FetchedAtUtc, s.Line, s.PriceA, s.PriceB, s.Source))
            .ToList();

        return points.Count == 0 ? null : new MarketMovement { Market = market, Points = points };
    }

    /// <summary>
    /// Thị trường nghiêng về cửa <paramref name="side"/> hay ngược lại:
    /// mức kèo dịch có lợi cho nhà cái về phía cửa đó (cửa đó phải chấp nhiều hơn / tài xỉu nâng lên),
    /// hoặc mức kèo đứng yên nhưng tiền kèo cửa đó giảm.
    /// </summary>
    public Lean LeanFor(PickSide side, decimal? minLineMove = null, decimal? minPriceDrop = null)
    {
        var lineThreshold = minLineMove ?? DefaultMinLineMove;
        var priceThreshold = minPriceDrop ?? DefaultMinPriceDrop;

        // Dương = thị trường đánh giá cửa này cao hơn lúc mở.
        var shift = side switch
        {
            PickSide.Home => -LineDelta,
            PickSide.Away => LineDelta,
            PickSide.Over => LineDelta,
            _ => -LineDelta,
        };

        var ownDrop = side is PickSide.Home or PickSide.Over ? PriceADrop : PriceBDrop;
        var otherDrop = side is PickSide.Home or PickSide.Over ? PriceBDrop : PriceADrop;

        if (shift != 0)
        {
            if (Math.Abs(shift) < lineThreshold)
            {
                return Lean.Neutral;
            }

            return shift > 0 ? Lean.Toward : Lean.Against;
        }

        if (ownDrop >= priceThreshold && ownDrop >= otherDrop)
        {
            return Lean.Toward;
        }

        if (otherDrop >= priceThreshold)
        {
            return Lean.Against;
        }

        return Lean.Neutral;
    }

    /// <summary>Mô tả bằng lời, vd "Kèo mở: MU chấp 3/4 trái (1.95 / 1.95) → hiện tại: MU chấp 1 trái (1.88 / 2.02). Kèo tăng, tiền đổ về MU."</summary>
    public string Describe(string home, string away, string currentLabel = "hiện tại")
    {
        if (Market == Market.AsianHandicap)
        {
            var open = $"{LineText.HandicapSentence(Open.Line, home, away)} ({LineText.Odds(Open.PriceA)} / {LineText.Odds(Open.PriceB)})";
            var current = $"{LineText.HandicapSentence(Current.Line, home, away)} ({LineText.Odds(Current.PriceA)} / {LineText.Odds(Current.PriceB)})";
            var lean = LeanFor(PickSide.Home);
            var summary = lean switch
            {
                Lean.Toward => LineDelta != 0 ? $"Mức chấp dịch {LineText.Handicap(LineDelta)} về phía {home} (kèo tăng cho {home}), tiền đổ về {home}." : $"Mức chấp đứng yên, tiền {home} giảm {LineText.Odds(PriceADrop)}: tiền đổ về {home}.",
                Lean.Against => LineDelta != 0 ? $"Mức chấp dịch {LineText.Handicap(LineDelta)} về phía {away} (kèo tăng cho {away}), tiền đổ về {away}." : $"Mức chấp đứng yên, tiền {away} giảm {LineText.Odds(PriceBDrop)}: tiền đổ về {away}.",
                _ => Changes == 0 ? "Chưa có biến động." : "Biến động nhỏ, chưa rõ xu hướng.",
            };
            return $"Kèo mở: {open} → {currentLabel}: {current}. {summary}";
        }
        else
        {
            var open = $"{LineText.Number(Open.Line)} (tài {LineText.Odds(Open.PriceA)} / xỉu {LineText.Odds(Open.PriceB)})";
            var current = $"{LineText.Number(Current.Line)} (tài {LineText.Odds(Current.PriceA)} / xỉu {LineText.Odds(Current.PriceB)})";
            var lean = LeanFor(PickSide.Over);
            var summary = lean switch
            {
                Lean.Toward => LineDelta != 0 ? $"Mức tài xỉu tăng {LineText.Number(LineDelta)}, nghiêng về Tài." : $"Mức đứng yên, tiền Tài giảm {LineText.Odds(PriceADrop)}: nghiêng về Tài.",
                Lean.Against => LineDelta != 0 ? $"Mức tài xỉu giảm {LineText.Number(-LineDelta)}, nghiêng về Xỉu." : $"Mức đứng yên, tiền Xỉu giảm {LineText.Odds(PriceBDrop)}: nghiêng về Xỉu.",
                _ => Changes == 0 ? "Chưa có biến động." : "Biến động nhỏ, chưa rõ xu hướng.",
            };
            return $"Tài xỉu mở: {open} → {currentLabel}: {current}. {summary}";
        }
    }

    /// <summary>Độ mạnh biến động để xếp hạng trang "Biến động kèo": dịch mức kèo nặng hơn dịch tiền.</summary>
    public decimal Magnitude => Math.Abs(LineDelta) * 4 + Math.Max(Math.Abs(PriceADrop), Math.Abs(PriceBDrop)) * 5;
}
