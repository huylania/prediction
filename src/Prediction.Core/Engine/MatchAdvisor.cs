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

            AddMarketSignal(item, ctx, home, away);
            AddApiSignal(item, ctx, home, away);
            advice.Picks.Add(item);
        }

        return advice;
    }

    private static void AddMarketSignal(PickAdvice item, MatchContext ctx, string home, string away)
    {
        var movement = ctx.MovementFor(item.Side.Market());
        if (movement is null)
        {
            return;
        }

        var name = LineText.Side(item.Side, home, away);
        var other = LineText.Side(item.Side.Opposite(), home, away);
        switch (movement.LeanFor(item.Side))
        {
            case Lean.Toward:
                item.For.Add($"Kèo/tiền đang nghiêng về {name}");
                break;
            case Lean.Against:
                item.Against.Add($"Kèo/tiền đang nghiêng về {other}");
                break;
        }
    }

    private static void AddApiSignal(PickAdvice item, MatchContext ctx, string home, string away)
    {
        var api = ctx.Advice;
        if (api is null)
        {
            return;
        }

        if (item.Side.Market() == Market.AsianHandicap)
        {
            if (api.WinnerTeamId is not { } winner)
            {
                return;
            }

            var sideTeam = item.Side == PickSide.Home ? ctx.Fixture.HomeTeamId : ctx.Fixture.AwayTeamId;
            var sidePercent = item.Side == PickSide.Home ? api.PercentHome : api.PercentAway;
            var name = LineText.Side(item.Side, home, away);
            var other = LineText.Side(item.Side.Opposite(), home, away);
            if (winner == sideTeam)
            {
                item.For.Add(sidePercent is { } p ? $"API dự đoán {name} thắng ({p}%)" : $"API dự đoán {name} thắng");
            }
            else
            {
                item.Against.Add($"API dự đoán {other} thắng");
            }
        }
        else if (api.UnderOverSide is { } ouSide)
        {
            var text = $"API dự đoán {(ouSide == PickSide.Over ? "Tài" : "Xỉu")} {api.UnderOver!.TrimStart('+', '-')}";
            (ouSide == item.Side ? item.For : item.Against).Add(text);
        }
    }

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

        if (api.WinnerTeamId is { } winner)
        {
            parts.Add($"Dự đoán thắng: {(winner == fixture.HomeTeamId ? home : winner == fixture.AwayTeamId ? away : "?")}");
        }

        if (api.UnderOverSide is { } ou)
        {
            parts.Add($"Tài/xỉu: {(ou == PickSide.Over ? "Tài" : "Xỉu")} {api.UnderOver!.TrimStart('+', '-')}");
        }

        return parts.Count == 0 ? null : string.Join(". ", parts);
    }
}
