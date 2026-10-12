using System.Globalization;
using Prediction.Core.Entities;

namespace Prediction.Web.Components.Shared;

/// <summary>Định dạng số cho giao diện (dấu chấm thập phân như bảng kèo).</summary>
public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Profit(decimal value) => value.ToString("+0.00;-0.00;0.00", Inv);

    public static string Pct(decimal? value) => value is null ? "—" : (value.Value * 100).ToString("0.0", Inv) + "%";

    public static string Roi(decimal? value) => value is null ? "—" : (value.Value * 100).ToString("+0.0;-0.0;0.0", Inv) + "%";

    public static string Odds(decimal? value) => value is null ? "—" : value.Value.ToString("0.00", Inv);

    public static string Num(decimal? value, string format = "0.##") => value is null ? "—" : value.Value.ToString(format, Inv);

    public static string Sign(decimal value) => value > 0 ? "text-success" : value < 0 ? "text-danger" : "text-muted";

    public static string OutcomeCss(BetOutcome outcome) => outcome switch
    {
        BetOutcome.Win => "bg-success",
        BetOutcome.HalfWin => "bg-success bg-opacity-75",
        BetOutcome.Push => "bg-secondary",
        BetOutcome.HalfLoss => "bg-danger bg-opacity-75",
        BetOutcome.Loss => "bg-danger",
        BetOutcome.Void => "bg-light text-dark border",
        _ => "bg-warning text-dark",
    };
}
