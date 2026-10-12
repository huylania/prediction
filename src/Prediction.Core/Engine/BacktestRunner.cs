using Prediction.Core.Entities;

namespace Prediction.Core.Engine;

public sealed class OutcomeCounts
{
    public int Win { get; private set; }
    public int HalfWin { get; private set; }
    public int Push { get; private set; }
    public int HalfLoss { get; private set; }
    public int Loss { get; private set; }

    public int Total => Win + HalfWin + Push + HalfLoss + Loss;

    public void Add(BetOutcome outcome)
    {
        switch (outcome)
        {
            case BetOutcome.Win: Win++; break;
            case BetOutcome.HalfWin: HalfWin++; break;
            case BetOutcome.Push: Push++; break;
            case BetOutcome.HalfLoss: HalfLoss++; break;
            case BetOutcome.Loss: Loss++; break;
        }
    }

    public decimal Share(int count) => Total == 0 ? 0 : (decimal)count / Total;

    /// <summary>Tỉ lệ thắng tính cả thắng nửa (= 0.5) trên các kèo không hòa.</summary>
    public decimal? WinRate
    {
        get
        {
            var won = Win + HalfWin * 0.5m;
            var lost = Loss + HalfLoss * 0.5m;
            return won + lost == 0 ? null : won / (won + lost);
        }
    }

    /// <summary>Odds (decimal) tối thiểu để cửa này hòa vốn với phân bố kết quả hiện có.</summary>
    public decimal? BreakEvenOdds
    {
        get
        {
            var won = Win + HalfWin * 0.5m;
            var lost = Loss + HalfLoss * 0.5m;
            return won == 0 ? null : 1 + lost / won;
        }
    }
}

public sealed record LineCoverRow(decimal Line, OutcomeCounts Counts);

public sealed record BacktestBet(Fixture Fixture, Pick Pick, SettlementResult Result, decimal Cumulative);

public sealed class BacktestReport
{
    // --- Thống kê kết quả (không cần kèo) ---

    /// <summary>Số trận khớp các điều kiện đội/BXH/phong độ.</summary>
    public int ResultMatches { get; set; }

    /// <summary>Quy tắc chỉ dựa vào kèo nên không có thống kê không cần kèo.</summary>
    public bool ResultStatsUnavailable { get; set; }

    /// <summary>Thắng/hòa/thua của cửa được chọn (chỉ với quy tắc kèo chấp).</summary>
    public int SideWins { get; set; }
    public int SideDraws { get; set; }
    public int SideLosses { get; set; }

    /// <summary>Kèo chấp: hiệu số của cửa được chọn (-4..+4, chặn hai đầu). Tài xỉu: tổng bàn (0..7+).</summary>
    public SortedDictionary<int, int> Distribution { get; } = [];

    /// <summary>Tỉ lệ thắng kèo của cửa được chọn ở từng mức chấp giả định.</summary>
    public List<LineCoverRow> HandicapRows { get; } = [];

    /// <summary>Tỉ lệ thắng kèo ở từng mức tài xỉu (cửa được chọn nếu là quy tắc tài xỉu, cửa Tài nếu là quy tắc chấp).</summary>
    public List<LineCoverRow> TotalsRows { get; } = [];

    public PickSide TotalsSide { get; set; } = PickSide.Over;

    // --- Lợi nhuận theo kèo thật ---

    public List<BacktestBet> Bets { get; } = [];

    public OutcomeCounts BetCounts { get; } = new();

    public decimal Profit { get; set; }

    public decimal? Roi => Bets.Count == 0 ? null : Profit / Bets.Count;

    public int LongestLosingStreak { get; set; }

    public decimal MaxDrawdown { get; set; }

    /// <summary>Số trận đã đá có kèo đóng của loại kèo quy tắc dùng.</summary>
    public int FixturesWithOdds { get; set; }
}

public static class BacktestRunner
{
    public static readonly decimal[] HandicapLines = Enumerable.Range(-8, 17).Select(i => i * 0.25m).ToArray();

    public static readonly decimal[] TotalLines = Enumerable.Range(6, 13).Select(i => i * 0.25m).ToArray();

    /// <summary>
    /// Chạy kiểm chứng trên các trận ĐÃ ĐÁ. Mỗi context phải được dựng tại thời điểm bóng lăn
    /// (BXH trước trận, kèo đóng, tư vấn API lấy trước trận).
    /// </summary>
    public static BacktestReport Run(Rule rule, IEnumerable<MatchContext> finished)
    {
        var report = new BacktestReport
        {
            TotalsSide = rule.Market == Market.OverUnder && rule.Selection == RuleSelection.Under ? PickSide.Under : PickSide.Over,
        };

        var handicapCounts = HandicapLines.Select(l => new LineCoverRow(l, new OutcomeCounts())).ToList();
        var totalCounts = TotalLines.Select(l => new LineCoverRow(l, new OutcomeCounts())).ToList();
        var cumulative = 0m;
        var peak = 0m;
        var losingStreak = 0;

        foreach (var ctx in finished.Where(c => c.Fixture.IsFinished).OrderBy(c => c.Fixture.KickoffUtc).ThenBy(c => c.Fixture.Id))
        {
            var hg = ctx.Fixture.HomeGoals!.Value;
            var ag = ctx.Fixture.AwayGoals!.Value;

            var tablePick = RuleEvaluator.Evaluate(rule, ctx, EvaluationMode.TableOnly);
            if (tablePick is not null)
            {
                report.ResultMatches++;
                if (rule.Market == Market.AsianHandicap)
                {
                    var margin = tablePick.Side == PickSide.Home ? hg - ag : ag - hg;
                    if (margin > 0) report.SideWins++;
                    else if (margin == 0) report.SideDraws++;
                    else report.SideLosses++;

                    var bucket = Math.Clamp(margin, -4, 4);
                    report.Distribution[bucket] = report.Distribution.GetValueOrDefault(bucket) + 1;

                    foreach (var row in handicapCounts)
                    {
                        row.Counts.Add(Settlement.SettleMargin(margin, row.Line, 2m).Outcome);
                    }
                }
                else
                {
                    var bucket = Math.Min(hg + ag, 7);
                    report.Distribution[bucket] = report.Distribution.GetValueOrDefault(bucket) + 1;
                }

                foreach (var row in totalCounts)
                {
                    row.Counts.Add(Settlement.Settle(report.TotalsSide, row.Line, 2m, hg, ag).Outcome);
                }
            }

            if (ctx.MovementFor(rule.Market) is null)
            {
                continue;
            }

            report.FixturesWithOdds++;
            var pick = RuleEvaluator.Evaluate(rule, ctx);
            if (pick is null)
            {
                continue;
            }

            var result = Settlement.Settle(pick.Side, pick.Line, pick.Odds, hg, ag);
            cumulative += result.Profit;
            peak = Math.Max(peak, cumulative);
            report.MaxDrawdown = Math.Max(report.MaxDrawdown, peak - cumulative);
            losingStreak = result.Profit < 0 ? losingStreak + 1 : result.Profit > 0 ? 0 : losingStreak;
            report.LongestLosingStreak = Math.Max(report.LongestLosingStreak, losingStreak);
            report.BetCounts.Add(result.Outcome);
            report.Bets.Add(new BacktestBet(ctx.Fixture, pick, result, cumulative));
        }

        report.Profit = cumulative;
        report.ResultStatsUnavailable = rule.Market == Market.AsianHandicap && !rule.HasSubjectConditions;
        if (rule.Market == Market.AsianHandicap)
        {
            report.HandicapRows.AddRange(handicapCounts);
        }

        report.TotalsRows.AddRange(totalCounts);
        return report;
    }
}
