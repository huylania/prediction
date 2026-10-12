using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Prediction.Core.Entities;

namespace Prediction.Web.ApiFootball;

/// <summary>Một mức kèo hai cửa: Line theo đội nhà (chấp) hoặc tổng bàn (tài xỉu).</summary>
public sealed record ParsedQuote(Market Market, decimal Line, decimal PriceA, decimal PriceB);

/// <summary>
/// Đọc kèo chấp châu Á ("Asian Handicap") và tài xỉu ("Goals Over/Under") từ phản hồi /odds,
/// chọn MỨC CHÍNH = cặp có hai odds gần nhau nhất.
/// </summary>
public static partial class OddsParser
{
    public const string HandicapBetName = "Asian Handicap";
    public const string TotalsBetName = "Goals Over/Under";

    // Tổng nghịch đảo odds của một cặp kèo hai cửa thật (1 + lợi nhuận nhà cái).
    private const decimal MinOverround = 0.98m;
    private const decimal MaxOverround = 1.25m;

    [GeneratedRegex(@"^(Home|Away)\s*([+-]?\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase)]
    private static partial Regex HandicapValue();

    [GeneratedRegex(@"^(Over|Under)\s*\+?(\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase)]
    private static partial Regex TotalsValue();

    public static IReadOnlyList<ParsedQuote> Parse(OddsItem item, int bookmakerId)
    {
        var bookmaker = item.Bookmakers.FirstOrDefault(b => b.Id == bookmakerId) ?? item.Bookmakers.FirstOrDefault();
        if (bookmaker is null)
        {
            return [];
        }

        var quotes = new List<ParsedQuote>(2);
        var handicap = bookmaker.Bets.FirstOrDefault(b => string.Equals(b.Name?.Trim(), HandicapBetName, StringComparison.OrdinalIgnoreCase));
        if (handicap is not null && MainHandicap(handicap.Values) is { } h)
        {
            quotes.Add(h);
        }

        var totals = bookmaker.Bets.FirstOrDefault(b => string.Equals(b.Name?.Trim(), TotalsBetName, StringComparison.OrdinalIgnoreCase));
        if (totals is not null && MainTotals(totals.Values) is { } t)
        {
            quotes.Add(t);
        }

        return quotes;
    }

    /// <summary>
    /// API có thể ghi mức chấp theo hai kiểu: "Home -0.75" / "Away +0.75" (mỗi cửa mang mức của chính nó)
    /// hoặc "Home -0.75" / "Away -0.75" (cùng một con số = mức của đội nhà). Thử cả hai cách ghép,
    /// giữ cách cho nhiều cặp hợp lệ hơn (tổng xác suất ngầm định giống kèo thật).
    /// </summary>
    public static ParsedQuote? MainHandicap(IEnumerable<BetValue> values)
    {
        var home = new Dictionary<decimal, decimal>();
        var away = new Dictionary<decimal, decimal>();
        foreach (var v in values)
        {
            var match = HandicapValue().Match(Text(v.Value));
            if (!match.Success || !TryLine(match.Groups[2].Value, out var line) || !TryOdds(v.Odd, out var odds))
            {
                continue;
            }

            var target = match.Groups[1].Value.Equals("Home", StringComparison.OrdinalIgnoreCase) ? home : away;
            target.TryAdd(line, odds);
        }

        var mirrored = Pairs(home, h => away.TryGetValue(-h, out var a) ? a : null);
        var sameNumber = Pairs(home, h => away.TryGetValue(h, out var a) ? a : null);
        var chosen = sameNumber.Count > mirrored.Count ? sameNumber : mirrored;

        return MainLine(chosen, Market.AsianHandicap);
    }

    public static ParsedQuote? MainTotals(IEnumerable<BetValue> values)
    {
        var over = new Dictionary<decimal, decimal>();
        var under = new Dictionary<decimal, decimal>();
        foreach (var v in values)
        {
            var match = TotalsValue().Match(Text(v.Value));
            if (!match.Success || !TryLine(match.Groups[2].Value, out var line) || !TryOdds(v.Odd, out var odds))
            {
                continue;
            }

            var target = match.Groups[1].Value.Equals("Over", StringComparison.OrdinalIgnoreCase) ? over : under;
            target.TryAdd(line, odds);
        }

        return MainLine(Pairs(over, l => under.TryGetValue(l, out var u) ? u : null), Market.OverUnder);
    }

    private static List<(decimal Line, decimal A, decimal B)> Pairs(Dictionary<decimal, decimal> sideA, Func<decimal, decimal?> findB)
    {
        var pairs = new List<(decimal, decimal, decimal)>();
        foreach (var (line, a) in sideA)
        {
            if (findB(line) is not { } b)
            {
                continue;
            }

            var overround = 1 / a + 1 / b;
            if (overround is >= MinOverround and <= MaxOverround)
            {
                pairs.Add((line, a, b));
            }
        }

        return pairs;
    }

    private static ParsedQuote? MainLine(List<(decimal Line, decimal A, decimal B)> pairs, Market market)
    {
        if (pairs.Count == 0)
        {
            return null;
        }

        var best = pairs.OrderBy(p => Math.Abs(p.A - p.B)).ThenBy(p => Math.Abs(p.Line)).First();
        return new ParsedQuote(market, best.Line, best.A, best.B);
    }

    private static string Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!.Trim(),
        JsonValueKind.Number => value.GetRawText(),
        _ => "",
    };

    private static bool TryLine(string text, out decimal line)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out line))
        {
            return false;
        }

        line = line == 0 ? 0 : line; // chuẩn hóa "-0"
        return line * 4 == decimal.Truncate(line * 4);
    }

    private static bool TryOdds(JsonElement value, out decimal odds)
    {
        odds = 0;
        var ok = value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetDecimal(out odds),
            JsonValueKind.String => decimal.TryParse(value.GetString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out odds),
            _ => false,
        };

        return ok && odds > 1;
    }
}
