using System.Globalization;
using Prediction.Core.Entities;

namespace Prediction.Web.ApiFootball;

public sealed record LiveCornerQuote(int FixtureId, CornerWindow Window, decimal Line, decimal OverOdds, decimal UnderOdds, int? Minute);

/// <summary>
/// Kèo chấp châu Á / tài xỉu bàn đang đá. Line kèo chấp theo đội nhà và tính trên tỉ số CẢ TRẬN (gồm bàn đã ghi);
/// PriceA = nhà/tài, PriceB = khách/xỉu; kèm tỉ số lúc báo kèo.
/// </summary>
public sealed record LiveGoalQuote(int FixtureId, Market Market, bool FirstHalf, decimal Line, decimal PriceA, decimal PriceB, int? Minute,
    int? HomeGoals = null, int? AwayGoals = null);

/// <summary>
/// Đọc /odds/live: kèo góc (cả trận "Asian Corners", dự phòng "Total Corners"; hiệp 1 "Corners Asian Handicap (1st Half)")
/// và kèo bàn ("Asian Handicap", "Over/Under Line" và bản hiệp 1). Mức chính = mức nhà cái đánh dấu main, không thì cặp odds gần nhau nhất.
/// </summary>
public static class LiveOddsParser
{
    public const string FullTimeAsian = "Asian Corners";
    public const string FullTimeTotal = "Total Corners";

    /// <summary>Tên API đặt là "Asian Handicap" nhưng giá trị là Tài/Xỉu tổng góc hiệp 1.</summary>
    public const string FirstHalf = "Corners Asian Handicap (1st Half)";

    public const string Handicap = "Asian Handicap";
    public const string HandicapFirstHalf = "Asian Handicap (1st Half)";

    /// <summary>Tài xỉu châu Á (có mức 1/4); dự phòng "Match Goals" (chỉ mức x.5).</summary>
    public const string TotalsLine = "Over/Under Line";
    public const string TotalsGoals = "Match Goals";
    public const string TotalsLineFirstHalf = "Over/Under Line (1st Half)";
    public const string TotalsFirstHalf = "Over/Under (1st Half)";

    // Tổng nghịch đảo odds của một cặp kèo hai cửa thật (giống OddsParser).
    private const decimal MinOverround = 0.98m;
    private const decimal MaxOverround = 1.25m;

    public static List<LiveCornerQuote> Parse(LiveOddsItem item)
    {
        var quotes = new List<LiveCornerQuote>();
        var minute = item.Fixture.Status.Elapsed;
        if ((MainTotals(item, FullTimeAsian) ?? MainTotals(item, FullTimeTotal)) is { } ft)
        {
            quotes.Add(new LiveCornerQuote(item.Fixture.Id, CornerWindow.SecondHalf, ft.Line, ft.A, ft.B, minute));
        }

        if (MainTotals(item, FirstHalf) is { } h1)
        {
            quotes.Add(new LiveCornerQuote(item.Fixture.Id, CornerWindow.FirstHalf, h1.Line, h1.A, h1.B, minute));
        }

        return quotes;
    }

    /// <summary>Kèo chấp và tài xỉu bàn cả trận, kèm bản hiệp 1 nếu nhà cái còn mở.</summary>
    public static List<LiveGoalQuote> ParseGoals(LiveOddsItem item)
    {
        var quotes = new List<LiveGoalQuote>(4);
        var minute = item.Fixture.Status.Elapsed;
        void Add(Market market, bool firstHalf, (decimal Line, decimal A, decimal B)? q)
        {
            if (q is { } x)
            {
                quotes.Add(new LiveGoalQuote(item.Fixture.Id, market, firstHalf, x.Line, x.A, x.B, minute, item.Teams?.Home?.Goals, item.Teams?.Away?.Goals));
            }
        }

        Add(Market.AsianHandicap, false, MainHandicap(item, Handicap));
        Add(Market.OverUnder, false, MainTotals(item, TotalsLine) ?? MainTotals(item, TotalsGoals));
        Add(Market.AsianHandicap, true, MainHandicap(item, HandicapFirstHalf));
        Add(Market.OverUnder, true, MainTotals(item, TotalsLineFirstHalf) ?? MainTotals(item, TotalsFirstHalf));
        return quotes;
    }

    /// <summary>Mức chính Tài/Xỉu: hai giá trị "Over"/"Under" cùng mức.</summary>
    private static (decimal Line, decimal A, decimal B)? MainTotals(LiveOddsItem item, string betName)
    {
        var values = Values(item, betName);
        var over = Side(values, "Over");
        var under = Side(values, "Under");
        return Best(over, line => under.GetValueOrDefault(line));
    }

    /// <summary>
    /// Mức chính kèo chấp. Dữ liệu live thật ghi kiểu đối xứng ("Home -0.25" / "Away 0.25"); vẫn thử kiểu cùng số
    /// ("Home -0.25" / "Away -0.25", như kèo trước trận) và lấy kiểu ghép được nhiều cặp hơn.
    /// </summary>
    private static (decimal Line, decimal A, decimal B)? MainHandicap(LiveOddsItem item, string betName)
    {
        var values = Values(item, betName);
        var home = Side(values, "Home");
        var away = Side(values, "Away");
        int Count(Func<decimal, decimal> partner) => home.Keys.Count(h => away.ContainsKey(partner(h)));
        Func<decimal, decimal> mirrored = h => h == 0 ? 0 : -h;
        Func<decimal, decimal> sameNumber = h => h;
        var partner = Count(mirrored) >= Count(sameNumber) ? mirrored : sameNumber;
        return Best(home, line => away.GetValueOrDefault(partner(line)));
    }

    private static List<(string Value, decimal Line, decimal Odds, bool Main)> Values(LiveOddsItem item, string betName)
    {
        var bet = item.Odds.FirstOrDefault(b => string.Equals(b.Name?.Trim(), betName, StringComparison.OrdinalIgnoreCase));
        if (bet is null)
        {
            return [];
        }

        return bet.Values
            .Where(v => !v.Suspended && v.Value is not null)
            .Select(v => (Value: v.Value!.Trim(), Line: Dec(v.Handicap), Odds: Dec(v.Odd), Main: v.Main == true))
            .Where(v => v.Line is { } l && l * 4 == decimal.Truncate(l * 4) && v.Odds is > 1m)
            .Select(v => (v.Value, v.Line!.Value == 0 ? 0m : v.Line!.Value, v.Odds!.Value, v.Main))
            .ToList();
    }

    private static Dictionary<decimal, (decimal Odds, bool Main)> Side(List<(string Value, decimal Line, decimal Odds, bool Main)> values, string side)
    {
        var map = new Dictionary<decimal, (decimal, bool)>();
        foreach (var v in values.Where(v => string.Equals(v.Value, side, StringComparison.OrdinalIgnoreCase)))
        {
            map.TryAdd(v.Line, (v.Odds, v.Main));
        }

        return map;
    }

    /// <summary>Cặp hợp lệ tốt nhất: ưu tiên mức main, rồi hai odds gần nhau nhất.</summary>
    private static (decimal Line, decimal A, decimal B)? Best(Dictionary<decimal, (decimal Odds, bool Main)> sideA, Func<decimal, (decimal Odds, bool Main)> findB)
    {
        var pairs = sideA
            .Select(kv => (Line: kv.Key, A: kv.Value, B: findB(kv.Key)))
            .Where(p => p.B.Odds > 1m && 1 / p.A.Odds + 1 / p.B.Odds is >= MinOverround and <= MaxOverround)
            .ToList();
        if (pairs.Count == 0)
        {
            return null;
        }

        var best = pairs
            .OrderByDescending(p => p.A.Main || p.B.Main)
            .ThenBy(p => Math.Abs(p.A.Odds - p.B.Odds))
            .ThenBy(p => Math.Abs(p.Line))
            .First();
        return (best.Line, best.A.Odds, best.B.Odds);
    }

    private static decimal? Dec(string? text) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
}
