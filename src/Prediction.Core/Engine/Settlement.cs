using Prediction.Core.Entities;

namespace Prediction.Core.Engine;

public readonly record struct SettlementResult(BetOutcome Outcome, decimal Profit);

/// <summary>Chấm kèo chấp châu Á và tài xỉu, gồm cả mức 1/4 và 3/4 (thắng/thua nửa).</summary>
public static class Settlement
{
    /// <summary>Tách mức 1/4, 3/4 thành hai nửa; mức tròn hoặc nửa trái giữ nguyên.</summary>
    public static (decimal First, decimal Second) SplitLine(decimal line)
    {
        var quarters = line * 4;
        if (quarters != decimal.Truncate(quarters))
        {
            throw new ArgumentException($"Mức kèo {line} không phải bội số của 0.25.", nameof(line));
        }

        return quarters % 2 == 0 ? (line, line) : (line - 0.25m, line + 0.25m);
    }

    /// <summary>Chấm một cửa với mức kèo nhìn từ phía cửa đó.</summary>
    public static SettlementResult Settle(PickSide side, decimal lineForSide, decimal odds, int homeGoals, int awayGoals)
    {
        // "margin" = số bàn cửa được chọn hơn đối phương sau khi tính kèo, trước khi cộng mức chấp.
        var (margin, line) = side switch
        {
            PickSide.Home => ((decimal)(homeGoals - awayGoals), lineForSide),
            PickSide.Away => (awayGoals - homeGoals, lineForSide),
            PickSide.Over => (homeGoals + awayGoals, -lineForSide),
            PickSide.Under => (-(homeGoals + awayGoals), lineForSide),
            _ => throw new ArgumentOutOfRangeException(nameof(side)),
        };

        return SettleMargin(margin, line, odds);
    }

    /// <summary>Kết quả kèo khi hiệu số (đã quy đổi về cửa được chọn) là <paramref name="margin"/>.</summary>
    public static SettlementResult SettleMargin(decimal margin, decimal line, decimal odds)
    {
        var (first, second) = SplitLine(line);
        var a = Math.Sign(margin + first);
        var b = Math.Sign(margin + second);

        var outcome = (a + b) switch
        {
            2 => BetOutcome.Win,
            1 => BetOutcome.HalfWin,
            0 when a == 0 => BetOutcome.Push,
            0 => throw new InvalidOperationException("Hai nửa kèo không thể vừa thắng vừa thua."),
            -1 => BetOutcome.HalfLoss,
            _ => BetOutcome.Loss,
        };

        return new SettlementResult(outcome, Profit(outcome, odds));
    }

    /// <summary>Kết quả tạm tính nếu trận dừng ở tỉ số hiện tại (null nếu chưa có tỉ số).</summary>
    public static SettlementResult? AtScore(PickSide side, decimal lineForSide, decimal odds, int? homeGoals, int? awayGoals) =>
        homeGoals is { } hg && awayGoals is { } ag ? Settle(side, lineForSide, odds, hg, ag) : null;

    public static decimal Profit(BetOutcome outcome, decimal odds) => outcome switch
    {
        BetOutcome.Win => odds - 1,
        BetOutcome.HalfWin => (odds - 1) / 2,
        BetOutcome.HalfLoss => -0.5m,
        BetOutcome.Loss => -1m,
        _ => 0m,
    };
}
