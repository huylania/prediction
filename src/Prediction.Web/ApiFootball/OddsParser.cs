using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Prediction.Core.Entities;

namespace Prediction.Web.ApiFootball;

/// <summary>Một mức kèo hai cửa: Line theo đội nhà (chấp) hoặc tổng bàn (tài xỉu).</summary>
public sealed record ParsedQuote(Market Market, decimal Line, decimal PriceA, decimal PriceB);

/// <summary>
/// Đọc kèo chấp châu Á ("Asian Handicap") và tài xỉu châu Á ("Goal Line", dự phòng "Goals Over/Under") từ phản hồi /odds,
/// chọn MỨC CHÍNH = cặp có hai odds gần nhau nhất.
/// </summary>
public static partial class OddsParser
{
    public const string HandicapBetName = "Asian Handicap";
    public const string TotalsBetName = "Goals Over/Under";

    /// <summary>Tài xỉu châu Á của Bet365 (có mức 1/4, 3/4); "Goals Over/Under" chỉ có mức x.5 nên mức chính hay lệch giá.</summary>
    public const string AsianTotalsBetName = "Goal Line";
    public const string CornersTotalsBetName = "Corners Over Under";
    public const string CornersHandicapBetName = "Corners Asian Handicap";
    public const string CardsTotalsBetName = "Cards Over/Under";

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

        var quotes = new List<ParsedQuote>(5);
        Add(HandicapBetName, values => MainHandicap(values, Market.AsianHandicap));

        // Tài xỉu bàn: gộp Goal Line (ưu tiên giá khi trùng mức) với Goals Over/Under rồi chọn mức chính.
        var totals = Values(AsianTotalsBetName).Concat(Values(TotalsBetName)).ToList();
        if (totals.Count > 0 && MainTotals(totals, Market.OverUnder) is { } ou)
        {
            quotes.Add(ou);
        }

        Add(CornersTotalsBetName, values => MainTotals(values, Market.CornersOverUnder));
        Add(CornersHandicapBetName, values => MainHandicap(values, Market.CornersHandicap));
        Add(CardsTotalsBetName, values => MainTotals(values, Market.CardsOverUnder));
        return quotes;

        void Add(string betName, Func<List<BetValue>, ParsedQuote?> parse)
        {
            var bet = bookmaker.Bets.FirstOrDefault(b => string.Equals(b.Name?.Trim(), betName, StringComparison.OrdinalIgnoreCase));
            if (bet is not null && parse(bet.Values) is { } quote)
            {
                quotes.Add(quote);
            }
        }

        List<BetValue> Values(string betName) =>
            bookmaker.Bets.FirstOrDefault(b => string.Equals(b.Name?.Trim(), betName, StringComparison.OrdinalIgnoreCase))?.Values ?? [];
    }

    /// <summary>
    /// API-Football ghi mức chấp kiểu "cùng số": "Home -0.75" / "Away -0.75" là một cặp, con số là mức chấp
    /// của đội nhà (đã xác nhận với dữ liệu Bet365 thật). Vẫn thử thêm kiểu đối xứng ("Home -0.75" / "Away +0.75")
    /// phòng khi nhà cái khác ghi khác, và giữ kiểu cho nhiều cặp hợp lệ hơn (tổng xác suất ngầm định giống kèo thật).
    /// </summary>
    public static ParsedQuote? MainHandicap(IEnumerable<BetValue> values, Market market = Market.AsianHandicap) =>
        MainLine(HandicapPairs(values), market);

    private static List<(decimal Line, decimal A, decimal B)> HandicapPairs(IEnumerable<BetValue> values)
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
        // API-Football thật (đã kiểm tra với Bet365) dùng kiểu "cùng số"; chỉ chọn kiểu đối xứng khi nó cho nhiều cặp hợp lệ hơn hẳn.
        return mirrored.Count > sameNumber.Count ? mirrored : sameNumber;
    }

    public static ParsedQuote? MainTotals(IEnumerable<BetValue> values, Market market = Market.OverUnder) =>
        MainLine(TotalsPairs(values), market);

    private static List<(decimal Line, decimal A, decimal B)> TotalsPairs(IEnumerable<BetValue> values)
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

        return Pairs(over, l => under.TryGetValue(l, out var u) ? u : null);
    }

    /// <summary>
    /// Mọi mức hợp lệ (chấp, tài xỉu bàn, tài xỉu góc) của đúng nhà cái <paramref name="bookmakerId"/> — không lấy nhà cái khác thay thế.
    /// Dùng cho kèo Pinnacle: so giá Bet365 với Pinnacle ở cùng mức.
    /// </summary>
    public static IReadOnlyList<(ParsedQuote Quote, bool IsMain)> Ladder(OddsItem item, int bookmakerId)
    {
        var bookmaker = item.Bookmakers.FirstOrDefault(b => b.Id == bookmakerId);
        if (bookmaker is null)
        {
            return [];
        }

        List<BetValue> Values(string betName) =>
            bookmaker.Bets.Where(b => string.Equals(b.Name?.Trim(), betName, StringComparison.OrdinalIgnoreCase)).SelectMany(b => b.Values).ToList();

        var result = new List<(ParsedQuote, bool)>();
        void Add(Market market, List<(decimal Line, decimal A, decimal B)> pairs)
        {
            var main = MainLine(pairs, market);
            result.AddRange(pairs.DistinctBy(p => p.Line).OrderBy(p => p.Line)
                .Select(p => (new ParsedQuote(market, p.Line, p.A, p.B), main is { } m && m.Line == p.Line)));
        }

        Add(Market.AsianHandicap, HandicapPairs(Values(HandicapBetName)));
        Add(Market.OverUnder, TotalsPairs(Values(AsianTotalsBetName).Concat(Values(TotalsBetName))));
        Add(Market.CornersOverUnder, TotalsPairs(Values(CornersTotalsBetName)));
        return result;
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
