using System.Globalization;
using Prediction.Core.Entities;

namespace Prediction.Core.Engine;

/// <summary>Thành tích thực tế của một quy tắc (từ các gợi ý đã chấm).</summary>
public sealed record RuleRecord(int Settled, int Wins, int HalfWins, int Pushes, int HalfLosses, int Losses, decimal Profit)
{
    public static readonly RuleRecord Empty = new(0, 0, 0, 0, 0, 0, 0);

    /// <summary>Lãi/lỗ trên tổng tiền cược (mỗi kèo 1 đơn vị).</summary>
    public decimal? Roi => Settled == 0 ? null : Profit / Settled;

    public string Summary =>
        Settled == 0
            ? "chưa có kèo đã chấm"
            : string.Create(CultureInfo.InvariantCulture, $"{Wins + HalfWins}T-{Pushes}H-{Losses + HalfLosses}B, ROI {Roi:+0.#%;-0.#%;0%}");
}

public sealed record RulePick(Rule Rule, Pick Pick, RuleRecord Record);

/// <summary>Tư vấn cho một cửa cụ thể của trận.</summary>
public sealed class PickAdvice
{
    public required PickSide Side { get; init; }

    public required decimal Line { get; init; }

    public required decimal Odds { get; init; }

    public required string Label { get; init; }

    public List<RulePick> Rules { get; } = [];

    public List<string> For { get; } = [];

    public List<string> Against { get; } = [];

    public string Consensus => $"{For.Count}/{For.Count + Against.Count} tín hiệu ủng hộ";

    public bool HasConflict => Against.Count > 0;
}

public sealed class MatchAdvice
{
    public List<PickAdvice> Picks { get; } = [];

    public string? HandicapMovement { get; init; }

    public string? TotalsMovement { get; init; }

    public string? ApiSummary { get; init; }
}

public enum TrendStrength
{
    /// <summary>Không có tín hiệu nào.</summary>
    None = 0,

    /// <summary>Một tín hiệu, không có tín hiệu ngược.</summary>
    Weak = 1,

    /// <summary>Từ hai tín hiệu cùng hướng, không có tín hiệu ngược.</summary>
    Strong = 2,

    /// <summary>Tín hiệu trái chiều.</summary>
    Mixed = 3,
}

/// <summary>Gợi ý cho một loại kèo (chấp hoặc tài xỉu) của một trận.</summary>
public sealed class MarketOutlook
{
    public required Market Market { get; init; }

    /// <summary>Biến động kèo đến hiện tại (hoặc kèo đóng nếu trận đã bắt đầu).</summary>
    public MarketMovement? Movement { get; init; }

    /// <summary>Cửa theo quy tắc của bạn có nhiều tín hiệu ủng hộ nhất.</summary>
    public PickAdvice? RulePick { get; init; }

    /// <summary>Các cửa khác cũng được quy tắc chọn (các quy tắc mâu thuẫn nhau).</summary>
    public IReadOnlyList<PickAdvice> OtherRulePicks { get; init; } = [];

    /// <summary>Xu hướng tham khảo từ biến động kèo + dự đoán API (không phải quy tắc của bạn).</summary>
    public PickSide? TrendSide { get; init; }
    public decimal? TrendLine { get; init; }
    public decimal? TrendOdds { get; init; }
    public TrendStrength Strength { get; init; }
    public IReadOnlyList<string> TrendFor { get; init; } = [];
    public IReadOnlyList<string> TrendAgainst { get; init; } = [];
}

/// <summary>
/// Gộp các tín hiệu cho một trận: quy tắc khớp (kèm thành tích), biến động kèo, dự đoán của API.
/// Chỉ tổng hợp tín hiệu, không phải mô hình xác suất.
/// </summary>
public static class MatchAdvisor
{
    /// <summary>Quy tắc có từ ngần này kèo đã chấm mà vẫn lỗ thì bị coi là tín hiệu ngược.</summary>
    public const int MinSettledForTrackRecord = 10;

    public static MatchAdvice Advise(MatchContext ctx, IEnumerable<RulePick> rulePicks, string home, string away)
    {
        var picks = rulePicks.ToList();
        var label = FixtureStatus.IsUpcoming(ctx.Fixture.Status) ? "hiện tại" : "kèo đóng";
        var advice = new MatchAdvice
        {
            HandicapMovement = ctx.Handicap?.Describe(home, away, label),
            TotalsMovement = ctx.Totals?.Describe(home, away, label),
            ApiSummary = ApiSummary(ctx.Advice, ctx.Fixture, home, away),
        };

        foreach (var group in picks.GroupBy(p => p.Pick.Side).OrderBy(g => g.Key))
        {
            var first = group.First().Pick;
            var item = new PickAdvice
            {
                Side = group.Key,
                Line = first.Line,
                Odds = first.Odds,
                Label = LineText.Pick(group.Key, first.Line, first.Odds, home, away),
            };
            item.Rules.AddRange(group);

            foreach (var rp in group)
            {
                item.For.Add($"Quy tắc “{rp.Rule.Name}” ({rp.Record.Summary})");
                if (rp.Record.Settled >= MinSettledForTrackRecord && rp.Record.Profit < 0)
                {
                    item.Against.Add($"Quy tắc “{rp.Rule.Name}” đang lỗ sau {rp.Record.Settled} kèo");
                }
            }

            foreach (var rp in picks.Where(p => p.Pick.Side == group.Key.Opposite()))
            {
                item.Against.Add($"Quy tắc “{rp.Rule.Name}” chọn cửa ngược lại");
            }

            AddSignals(ctx, item.Side, home, away, item.For, item.Against);
            advice.Picks.Add(item);
        }

        return advice;
    }

    /// <summary>
    /// Gợi ý cho một loại kèo của trận: cửa theo quy tắc (nếu có quy tắc khớp), và xu hướng tham khảo
    /// từ biến động kèo + dự đoán API cho mọi trận.
    /// </summary>
    public static MarketOutlook Outlook(MatchContext ctx, MatchAdvice advice, Market market, string home, string away)
    {
        var rulePicks = advice.Picks
            .Where(p => p.Side.Market() == market)
            .OrderByDescending(p => p.For.Count - p.Against.Count)
            .ThenByDescending(p => p.Rules.Count)
            .ToList();

        PickSide[] sides = market == Market.AsianHandicap ? [PickSide.Home, PickSide.Away] : [PickSide.Over, PickSide.Under];
        var best = sides
            .Select(side =>
            {
                List<string> pro = [], con = [];
                AddSignals(ctx, side, home, away, pro, con);
                return (Side: side, For: pro, Against: con);
            })
            .OrderByDescending(x => x.For.Count - x.Against.Count)
            .First();

        var strength = (best.For.Count, best.Against.Count) switch
        {
            (0, _) => TrendStrength.None,
            ( >= 2, 0) => TrendStrength.Strong,
            (_, 0) => TrendStrength.Weak,
            _ => TrendStrength.Mixed,
        };

        var movement = ctx.MovementFor(market);
        var trendSide = strength is TrendStrength.Weak or TrendStrength.Strong ? best.Side : (PickSide?)null;
        return new MarketOutlook
        {
            Market = market,
            Movement = movement,
            RulePick = rulePicks.FirstOrDefault(),
            OtherRulePicks = rulePicks.Skip(1).ToList(),
            Strength = strength,
            TrendSide = trendSide,
            TrendLine = trendSide is { } ts && movement is not null ? LineText.LineForSide(ts, movement.Current.Line) : null,
            TrendOdds = trendSide is { } to && movement is not null ? (to is PickSide.Home or PickSide.Over ? movement.Current.PriceA : movement.Current.PriceB) : null,
            TrendFor = strength == TrendStrength.None ? [] : best.For,
            TrendAgainst = strength == TrendStrength.None ? [] : best.Against,
        };
    }

    /// <summary>Tín hiệu từ biến động kèo và dự đoán API cho một cửa.</summary>
    private static void AddSignals(MatchContext ctx, PickSide side, string home, string away, List<string> pro, List<string> con)
    {
        var name = LineText.Side(side, home, away);
        var other = LineText.Side(side.Opposite(), home, away);

        switch (ctx.MovementFor(side.Market())?.LeanFor(side))
        {
            case Lean.Toward:
                pro.Add($"Kèo/tiền đang nghiêng về {name}");
                break;
            case Lean.Against:
                con.Add($"Kèo/tiền đang nghiêng về {other}");
                break;
        }

        var api = ctx.Advice;
        if (api is null)
        {
            return;
        }

        if (side.Market() == Market.AsianHandicap)
        {
            if (api.WinnerTeamId is not { } winner)
            {
                return;
            }

            var sideTeam = side == PickSide.Home ? ctx.Fixture.HomeTeamId : ctx.Fixture.AwayTeamId;
            var otherTeam = side == PickSide.Home ? ctx.Fixture.AwayTeamId : ctx.Fixture.HomeTeamId;
            var sidePercent = side == PickSide.Home ? api.PercentHome : api.PercentAway;
            var verdict = WinOrDraw(api) ? "thắng hoặc hòa" : "thắng";
            if (winner == sideTeam)
            {
                pro.Add(sidePercent is { } p ? $"API dự đoán {name} {verdict} (thắng {p}%)" : $"API dự đoán {name} {verdict}");
            }
            else if (winner == otherTeam)
            {
                con.Add($"API dự đoán {other} {verdict}");
            }
        }
        else if (api.UnderOverSide is { } ouSide)
        {
            var text = $"API dự đoán {(ouSide == PickSide.Over ? "Tài" : "Xỉu")} {api.UnderOver!.TrimStart('+', '-')}";
            (ouSide == side ? pro : con).Add(text);
        }
    }

    /// <summary>API ghi "Win or draw" khi chỉ dự đoán đội đó không thua.</summary>
    private static bool WinOrDraw(ApiAdvice api) =>
        api.WinnerComment?.Contains("draw", StringComparison.OrdinalIgnoreCase) == true;

    private static string? ApiSummary(ApiAdvice? api, Fixture fixture, string home, string away)
    {
        if (api is null)
        {
            return null;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(api.Advice))
        {
            parts.Add($"“{api.Advice}”");
        }

        if (api.PercentHome.HasValue || api.PercentDraw.HasValue || api.PercentAway.HasValue)
        {
            parts.Add($"{home} {api.PercentHome ?? 0}% · Hòa {api.PercentDraw ?? 0}% · {away} {api.PercentAway ?? 0}%");
        }

        if (api.WinnerTeamId is { } winner && (winner == fixture.HomeTeamId || winner == fixture.AwayTeamId))
        {
            parts.Add($"Dự đoán: {(winner == fixture.HomeTeamId ? home : away)} {(WinOrDraw(api) ? "thắng hoặc hòa" : "thắng")}");
        }

        if (api.UnderOverSide is { } ou)
        {
            parts.Add($"Tài/xỉu: {(ou == PickSide.Over ? "Tài" : "Xỉu")} {api.UnderOver!.TrimStart('+', '-')}");
        }

        return parts.Count == 0 ? null : string.Join(". ", parts);
    }
}
