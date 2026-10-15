using System.Globalization;
using Prediction.Core.Entities;

namespace Prediction.Core.Engine.Analysis;

/// <summary>Kèo chấp + tài xỉu tại một mốc (mở kèo, khi có đội hình, 5 phút trước trận, hiện tại).</summary>
public sealed record LinePoint(
    string Label,
    DateTime? AtUtc,
    decimal? HandicapLine,
    decimal? HandicapHome,
    decimal? HandicapAway,
    decimal? TotalLine,
    decimal? Over,
    decimal? Under,
    DateTime? BookmakerUpdatedAtUtc = null)
{
    public static LinePoint? FromMovements(string label, MarketMovement? handicap, MarketMovement? totals, bool open)
    {
        var h = open ? handicap?.Open : handicap?.Current;
        var t = open ? totals?.Open : totals?.Current;
        if (h is null && t is null)
        {
            return null;
        }

        DateTime? at = h is null ? t!.AtUtc : t is null ? h.AtUtc : (open ? (h.AtUtc < t.AtUtc ? h.AtUtc : t.AtUtc) : (h.AtUtc > t.AtUtc ? h.AtUtc : t.AtUtc));
        return new LinePoint(label, at, h?.Line, h?.PriceA, h?.PriceB, t?.Line, t?.PriceA, t?.PriceB);
    }

    public static LinePoint FromCheckpoint(string label, OddsCheckpoint c) =>
        new(label, c.AtUtc, c.HandicapLine, c.HandicapHome, c.HandicapAway, c.TotalLine, c.Over, c.Under, c.BookmakerUpdatedAtUtc);
}

public enum LineOrigin
{
    None = 0,

    /// <summary>Website tự ghi (kèo cuối trước giờ bóng lăn).</summary>
    Recorded = 1,

    Manual = 2,

    /// <summary>AI tra cứu web — cần kiểm tra.</summary>
    Ai = 3,
}

/// <summary>Một trận cũ (đối đầu hoặc phong độ) kèm kèo đóng nếu biết.</summary>
public sealed record HistoryLine(
    int FixtureId,
    DateTime KickoffUtc,
    string? LeagueName,
    int HomeTeamId,
    int AwayTeamId,
    string HomeName,
    string AwayName,
    int? HomeGoals,
    int? AwayGoals,
    decimal? HandicapLine,
    decimal? TotalLine,
    LineOrigin Origin,
    string? Note = null)
{
    public bool HasScore => HomeGoals.HasValue && AwayGoals.HasValue;

    /// <summary>Mức chấp nhìn từ phía một đội (âm = đội đó chấp).</summary>
    public decimal? HandicapFor(int teamId) => HandicapLine is { } l ? (teamId == HomeTeamId ? l : -l) : null;

    /// <summary>Kết quả kèo chấp của một đội.</summary>
    public BetOutcome? HandicapOutcome(int teamId) => HasScore && HandicapFor(teamId) is { } line
        ? Settlement.Settle(teamId == HomeTeamId ? PickSide.Home : PickSide.Away, line, 2m, HomeGoals!.Value, AwayGoals!.Value).Outcome
        : null;

    /// <summary>Kết quả cửa Tài.</summary>
    public BetOutcome? OverOutcome => HasScore && TotalLine is { } t
        ? Settlement.Settle(PickSide.Over, t, 2m, HomeGoals!.Value, AwayGoals!.Value).Outcome
        : null;
}

public enum OddsAlertKind
{
    /// <summary>Kèo trận này dịch ≥ 0.5 giữa các mốc.</summary>
    Movement = 1,

    /// <summary>Lệch ≥ 0.5 so với lần gặp gần nhất.</summary>
    LastMeeting = 2,

    /// <summary>Biên độ kèo của 5 lần gặp gần nhất &gt; 0.5.</summary>
    MeetingRange = 3,
}

public sealed record OddsAlert(OddsAlertKind Kind, Market Market, string Title, string Detail, decimal Delta);

/// <summary>Cảnh báo kèo chấp / tài xỉu biến động hoặc lệch lịch sử từ 0.5 trở lên.</summary>
public static class OddsAlerts
{
    public const decimal Threshold = 0.5m;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Kèo dịch ≥ 0.5 từ lúc mở tới mốc mới nhất (5 phút trước trận / khi có đội hình / hiện tại),
    /// và riêng đoạn sau khi có đội hình.
    /// </summary>
    public static List<OddsAlert> Movement(LinePoint? open, LinePoint? lineups, LinePoint? preKickoff, LinePoint? current, string home, string away)
    {
        var alerts = new List<OddsAlert>();
        if (open is null)
        {
            return alerts;
        }

        var points = new[] { lineups, preKickoff, current }.Where(p => p is not null).Select(p => p!).ToList();
        foreach (var market in new[] { Market.AsianHandicap, Market.OverUnder })
        {
            decimal? Line(LinePoint p) => market == Market.AsianHandicap ? p.HandicapLine : p.TotalLine;
            string Show(decimal line) => market == Market.AsianHandicap ? $"{home} {LineText.Signed(line)}" : LineText.Number(line);
            var name = market == Market.AsianHandicap ? "Kèo chấp" : "Tài xỉu";

            if (Line(open) is not { } start)
            {
                continue;
            }

            // Mốc xa nhất so với kèo mở.
            var far = points.Where(p => Line(p).HasValue).OrderByDescending(p => Math.Abs(Line(p)!.Value - start)).FirstOrDefault();
            if (far is not null && Line(far) is { } end && Math.Abs(end - start) >= Threshold)
            {
                var delta = end - start;
                alerts.Add(new OddsAlert(OddsAlertKind.Movement, market,
                    $"{name} {(delta > 0 ? "tăng" : "giảm")} {LineText.Number(Math.Abs(delta))}",
                    $"{name} từ {Show(start)} (mở kèo) → {Show(end)} ({far.Label.ToLowerInvariant()})" + Direction(market, delta, home, away) + "."
                    , delta));
            }

            // Riêng đoạn sau khi có đội hình.
            if (lineups is not null && Line(lineups) is { } atLineup)
            {
                var after = new[] { preKickoff, current }.Where(p => p is not null && Line(p).HasValue).Select(p => p!).FirstOrDefault();
                if (after is not null && Line(after) is { } later && Math.Abs(later - atLineup) >= Threshold)
                {
                    var delta = later - atLineup;
                    alerts.Add(new OddsAlert(OddsAlertKind.Movement, market,
                        $"{name} nhảy {LineText.Number(Math.Abs(delta))} sau khi có đội hình",
                        $"{name} {Show(atLineup)} lúc có đội hình → {Show(later)} ({after.Label.ToLowerInvariant()})" + Direction(market, delta, home, away) + ".",
                        delta));
                }
            }
        }

        return alerts;
    }

    /// <summary>
    /// So kèo hôm nay với các lần gặp trước (mới nhất trước; mức chấp quy về đội nhà hôm nay):
    /// lệch ≥ 0.5 so với lần gặp gần nhất có kèo, hoặc biên độ kèo 5 lần gặp gần nhất &gt; 0.5.
    /// </summary>
    public static List<OddsAlert> HeadToHead(IReadOnlyList<HistoryLine> meetings, int homeTeamId, decimal? handicap, decimal? total, string home, string away)
    {
        var alerts = new List<OddsAlert>();
        var recent = meetings.OrderByDescending(m => m.KickoffUtc).Take(5).ToList();

        var withAh = recent.Where(m => m.HandicapLine.HasValue).ToList();
        if (withAh.Count > 0 && handicap is { } h)
        {
            var last = withAh[0];
            var lastLine = last.HandicapFor(homeTeamId)!.Value;
            var delta = h - lastLine;
            if (Math.Abs(delta) >= Threshold)
            {
                var sameVenue = last.HomeTeamId == homeTeamId;
                alerts.Add(new OddsAlert(OddsAlertKind.LastMeeting, Market.AsianHandicap,
                    $"Kèo chấp lệch {LineText.Number(Math.Abs(delta))} so với lần gặp gần nhất",
                    $"Hôm nay {home} {LineText.Signed(h)}; lần gặp {last.KickoffUtc.ToString("dd/MM/yyyy", Inv)} ({last.HomeName} {Score(last)} {last.AwayName}) {home} {LineText.Signed(lastLine)}"
                    + (sameVenue ? "" : " (lần đó đổi sân)") + Origin(last) + ".",
                    delta));
            }
        }

        if (withAh.Count >= 2)
        {
            var lines = withAh.Select(m => m.HandicapFor(homeTeamId)!.Value).ToList();
            var range = lines.Max() - lines.Min();
            if (range > Threshold)
            {
                alerts.Add(new OddsAlert(OddsAlertKind.MeetingRange, Market.AsianHandicap,
                    $"Biên độ kèo chấp {withAh.Count} lần gặp gần nhất là {LineText.Number(range)}",
                    $"Mức chấp của {home} các lần gặp: {string.Join(", ", lines.Select(LineText.Signed))} (từ {LineText.Signed(lines.Min())} đến {LineText.Signed(lines.Max())}).",
                    range));
            }
        }

        var withOu = recent.Where(m => m.TotalLine.HasValue).ToList();
        if (withOu.Count > 0 && total is { } t)
        {
            var last = withOu[0];
            var delta = t - last.TotalLine!.Value;
            if (Math.Abs(delta) >= Threshold)
            {
                alerts.Add(new OddsAlert(OddsAlertKind.LastMeeting, Market.OverUnder,
                    $"Tài xỉu lệch {LineText.Number(Math.Abs(delta))} so với lần gặp gần nhất",
                    $"Hôm nay {LineText.Number(t)}; lần gặp {last.KickoffUtc.ToString("dd/MM/yyyy", Inv)} ({last.HomeName} {Score(last)} {last.AwayName}) {LineText.Number(last.TotalLine.Value)}{Origin(last)}.",
                    delta));
            }
        }

        if (withOu.Count >= 2)
        {
            var lines = withOu.Select(m => m.TotalLine!.Value).ToList();
            var range = lines.Max() - lines.Min();
            if (range > Threshold)
            {
                alerts.Add(new OddsAlert(OddsAlertKind.MeetingRange, Market.OverUnder,
                    $"Biên độ tài xỉu {withOu.Count} lần gặp gần nhất là {LineText.Number(range)}",
                    $"Mốc tài xỉu các lần gặp: {string.Join(", ", lines.Select(LineText.Number))}.",
                    range));
            }
        }

        return alerts;
    }

    private static string Score(HistoryLine m) => m.HasScore ? $"{m.HomeGoals}-{m.AwayGoals}" : "vs";

    private static string Origin(HistoryLine m) => m.Origin switch
    {
        LineOrigin.Ai => " — kèo do AI tra cứu, cần kiểm tra",
        LineOrigin.Manual => " — kèo nhập tay",
        _ => "",
    };

    private static string Direction(Market market, decimal delta, string home, string away) => market == Market.AsianHandicap
        ? delta < 0 ? $": thị trường nghiêng về {home}" : $": thị trường nghiêng về {away}"
        : delta > 0 ? ": thị trường nghiêng Tài" : ": thị trường nghiêng Xỉu";
}

/// <summary>Ghi chú về sân và thời tiết có thể ảnh hưởng trận đấu.</summary>
public static class MatchConditions
{
    public const int HighAltitudeM = 1500;
    public const int ExtremeAltitudeM = 2500;

    public static List<string> Notes(Venue? venue, FixtureWeather? weather, int? aiAltitudeM = null, string? aiSurface = null)
    {
        var notes = new List<string>();
        var altitude = venue?.ElevationM ?? aiAltitudeM;
        if (altitude >= ExtremeAltitudeM)
        {
            notes.Add($"Sân rất cao ({altitude} m): đội khách quen đá thấp dễ hụt hơi, lợi thế lớn cho chủ nhà.");
        }
        else if (altitude >= HighAltitudeM)
        {
            notes.Add($"Sân trên cao ({altitude} m): thể lực đội khách có thể bị ảnh hưởng.");
        }

        var surface = venue?.Surface ?? aiSurface;
        if (surface?.Contains("artificial", StringComparison.OrdinalIgnoreCase) == true || surface?.Contains("nhân tạo", StringComparison.OrdinalIgnoreCase) == true)
        {
            notes.Add("Sân cỏ nhân tạo: bóng nảy nhanh, đội nhà quen mặt sân có lợi.");
        }

        if (weather is not null)
        {
            if (weather.PrecipitationMm >= 2 || weather.PrecipitationProbability >= 70 && weather.WeatherCode is >= 61)
            {
                notes.Add("Dự báo mưa lớn giờ bóng lăn: mặt sân trơn, nhịp độ và độ chính xác chuyền bóng có thể giảm.");
            }

            if (weather.WindKph >= 35)
            {
                notes.Add($"Gió mạnh ({weather.WindKph:0} km/h): bóng bổng, tạt cánh, sút xa kém chính xác.");
            }

            if (weather.TemperatureC >= 30)
            {
                notes.Add($"Nắng nóng ({weather.TemperatureC:0}°C): nhịp độ dễ giảm, thể lực cuối trận đuối.");
            }
            else if (weather.TemperatureC <= 0 || weather.WeatherCode is >= 71 and <= 77 or 85 or 86)
            {
                notes.Add("Trời rất lạnh / có tuyết: mặt sân nặng, khó chơi kỹ thuật.");
            }
        }

        return notes;
    }
}
