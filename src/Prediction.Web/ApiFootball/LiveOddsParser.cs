using System.Globalization;
using Prediction.Core.Entities;

namespace Prediction.Web.ApiFootball;

public sealed record LiveCornerQuote(int FixtureId, CornerWindow Window, decimal Line, decimal OverOdds, decimal UnderOdds, int? Minute);

/// <summary>Đọc kèo tài xỉu góc từ /odds/live: cả trận ("Asian Corners", dự phòng "Total Corners") và hiệp 1 ("Corners Asian Handicap (1st Half)").</summary>
public static class LiveOddsParser
{
    public const string FullTimeAsian = "Asian Corners";
    public const string FullTimeTotal = "Total Corners";

    /// <summary>Tên API đặt là "Asian Handicap" nhưng giá trị là Tài/Xỉu tổng góc hiệp 1.</summary>
    public const string FirstHalf = "Corners Asian Handicap (1st Half)";

    public static List<LiveCornerQuote> Parse(LiveOddsItem item)
    {
        var quotes = new List<LiveCornerQuote>();
        var minute = item.Fixture.Status.Elapsed;
        if ((MainLine(item, FullTimeAsian) ?? MainLine(item, FullTimeTotal)) is { } ft)
        {
            quotes.Add(new LiveCornerQuote(item.Fixture.Id, CornerWindow.SecondHalf, ft.Line, ft.Over, ft.Under, minute));
        }

        if (MainLine(item, FirstHalf) is { } h1)
        {
            quotes.Add(new LiveCornerQuote(item.Fixture.Id, CornerWindow.FirstHalf, h1.Line, h1.Over, h1.Under, minute));
        }

        return quotes;
    }

    /// <summary>Mức chính: cặp Tài/Xỉu (không bị khóa) có hai odds gần nhau nhất.</summary>
    private static (decimal Line, decimal Over, decimal Under)? MainLine(LiveOddsItem item, string betName)
    {
        var bet = item.Odds.FirstOrDefault(b => string.Equals(b.Name, betName, StringComparison.OrdinalIgnoreCase));
        if (bet is null)
        {
            return null;
        }

        var pairs = bet.Values
            .Where(v => !v.Suspended && Dec(v.Handicap) is not null && Dec(v.Odd) is > 1m)
            .GroupBy(v => Dec(v.Handicap)!.Value)
            .Select(g => (Line: g.Key,
                Over: g.Where(v => string.Equals(v.Value, "Over", StringComparison.OrdinalIgnoreCase)).Select(v => Dec(v.Odd)).FirstOrDefault(),
                Under: g.Where(v => string.Equals(v.Value, "Under", StringComparison.OrdinalIgnoreCase)).Select(v => Dec(v.Odd)).FirstOrDefault(),
                IsMain: g.Any(v => v.Main == true)))
            .Where(p => p.Over is not null && p.Under is not null && p.Line * 4 == decimal.Truncate(p.Line * 4))
            .ToList();
        if (pairs.Count == 0)
        {
            return null;
        }

        var best = pairs.OrderByDescending(p => p.IsMain).ThenBy(p => Math.Abs(p.Over!.Value - p.Under!.Value)).First();
        return (best.Line, best.Over!.Value, best.Under!.Value);
    }

    private static decimal? Dec(string? text) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
}
