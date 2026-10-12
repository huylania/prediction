using System.Globalization;
using Prediction.Core.Entities;

namespace Prediction.Core.Engine;

/// <summary>Định dạng mức kèo theo cách gọi quen thuộc.</summary>
public static class LineText
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Số có dấu: -0.75, +0.25, 0.</summary>
    public static string Signed(decimal line) =>
        line == 0 ? "0" : line.ToString(line > 0 ? "+0.##" : "0.##", Invariant);

    public static string Number(decimal value) => value.ToString("0.##", Invariant);

    public static string Odds(decimal odds) => odds.ToString("0.00", Invariant);

    /// <summary>Độ lớn mức chấp theo trái: 0.75 → "3/4", 1.25 → "1 1/4", 0 → "đồng banh".</summary>
    public static string Handicap(decimal magnitude)
    {
        magnitude = Math.Abs(magnitude);
        if (magnitude == 0)
        {
            return "đồng banh";
        }

        var whole = (int)decimal.Truncate(magnitude);
        var fraction = (magnitude - whole) switch
        {
            0.25m => "1/4",
            0.5m => "1/2",
            0.75m => "3/4",
            0m => "",
            var other => other.ToString("0.##", Invariant),
        };

        var text = whole == 0 ? fraction : fraction == "" ? whole.ToString(Invariant) : $"{whole} {fraction}";
        return $"{text} trái";
    }

    /// <summary>"MU chấp 3/4 trái" / "Đồng banh" theo mức chấp của đội nhà.</summary>
    public static string HandicapSentence(decimal homeLine, string home, string away)
    {
        if (homeLine == 0)
        {
            return "Đồng banh";
        }

        var favourite = homeLine < 0 ? home : away;
        return $"{favourite} chấp {Handicap(homeLine)}";
    }

    /// <summary>Mức kèo nhìn từ một cửa: Home dùng mức đội nhà, Away đảo dấu, tài xỉu giữ nguyên.</summary>
    public static decimal LineForSide(PickSide side, decimal storedLine) =>
        side == PickSide.Away ? -storedLine : storedLine;

    public static string Side(PickSide side, string home, string away) => side switch
    {
        PickSide.Home => home,
        PickSide.Away => away,
        PickSide.Over => "Tài",
        _ => "Xỉu",
    };

    /// <summary>"MU -0.75 @1.95" hoặc "Tài 2.5 @1.90".</summary>
    public static string Pick(PickSide side, decimal lineForSide, decimal odds, string home, string away) =>
        side.Market() == Market.AsianHandicap
            ? $"{Side(side, home, away)} {Signed(lineForSide)} @{Odds(odds)}"
            : $"{Side(side, home, away)} {Number(lineForSide)} @{Odds(odds)}";

    public static string Outcome(BetOutcome outcome) => outcome switch
    {
        BetOutcome.Pending => "Chờ",
        BetOutcome.Win => "Thắng",
        BetOutcome.HalfWin => "Thắng nửa",
        BetOutcome.Push => "Hòa",
        BetOutcome.HalfLoss => "Thua nửa",
        BetOutcome.Loss => "Thua",
        _ => "Hủy",
    };
}
