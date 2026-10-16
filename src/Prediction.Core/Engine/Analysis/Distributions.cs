using Prediction.Core.Entities;

namespace Prediction.Core.Engine.Analysis;

/// <summary>Xác suất các kết quả của một cửa kèo (thắng, thắng nửa, hoàn, thua nửa, thua).</summary>
public sealed record PickProbability(double Win, double HalfWin, double Push, double HalfLoss, double Loss)
{
    /// <summary>Xác suất thắng ít nhất nửa tiền.</summary>
    public double WinAny => Win + HalfWin;

    /// <summary>Xác suất thua ít nhất nửa tiền.</summary>
    public double LossAny => Loss + HalfLoss;

    /// <summary>Lợi thế ròng không tính odds: thắng (nửa tính nửa) trừ thua (nửa tính nửa), từ -1 đến 1.</summary>
    public double Net => Win + HalfWin / 2 - Loss - HalfLoss / 2;

    /// <summary>Lời/lỗ kỳ vọng trên 1 đơn vị tiền cược với odds thập phân <paramref name="odds"/>.</summary>
    public double ExpectedValue(decimal odds)
    {
        var o = (double)odds;
        return Win * (o - 1) + HalfWin * (o - 1) / 2 - HalfLoss / 2 - Loss;
    }

    /// <summary>
    /// Tỉ lệ "ăn" quy đổi (= 1 / odds hòa vốn): phần thắng / (phần thắng + phần thua), nửa tính nửa, hoàn tiền bỏ ra.
    /// So trực tiếp được với xác suất nhà cái đã bỏ lời. null nếu không có cả thắng lẫn thua.
    /// </summary>
    public double? Share
    {
        get
        {
            var win = Win + HalfWin / 2;
            var loss = Loss + HalfLoss / 2;
            return win + loss <= 1e-9 ? null : win / (win + loss);
        }
    }

    /// <summary>
    /// Phân phối mới có tỉ lệ ăn quy đổi = <paramref name="share"/>: nhân phần thắng và phần thua với hai hệ số,
    /// giữ nguyên xác suất hoàn tiền và tỉ lệ thắng nửa / thua nửa trong từng phía.
    /// </summary>
    public PickProbability WithShare(double share)
    {
        share = Math.Clamp(share, 0.01, 0.99);
        var a = Win + HalfWin / 2;
        var c = Loss + HalfLoss / 2;
        if (a <= 1e-9 || c <= 1e-9)
        {
            return this;
        }

        var winMass = Win + HalfWin;
        var lossMass = Loss + HalfLoss;
        var ratio = a * (1 - share) / (share * c); // beta = alpha * ratio
        var alpha = (winMass + lossMass) / (winMass + ratio * lossMass);
        var beta = alpha * ratio;
        return new PickProbability(Win * alpha, HalfWin * alpha, Push, HalfLoss * beta, Loss * beta);
    }

    /// <summary>Xác suất nhà cái cho một cửa sau khi bỏ phần lời (từ odds hai cửa).</summary>
    public static double? NoVig(decimal odds, decimal otherOdds) =>
        odds > 1 && otherOdds > 1 ? (double)(1 / odds / (1 / odds + 1 / otherOdds)) : null;
}

/// <summary>Phân phối rời rạc của số đếm (bàn, góc, thẻ) và cách chấm kèo trên phân phối đó.</summary>
public static class Distributions
{
    public const int MaxGoals = 10;
    public const int MaxCorners = 30;
    public const int MaxCards = 16;

    public static double[] Poisson(double mean, int max)
    {
        mean = Math.Max(mean, 0.01);
        var p = new double[max + 1];
        p[0] = Math.Exp(-mean);
        for (var k = 1; k <= max; k++)
        {
            p[k] = p[k - 1] * mean / k;
        }

        return Normalize(p);
    }

    /// <summary>
    /// Nhị thức âm với phương sai = <paramref name="dispersion"/> × trung bình (góc, thẻ biến động hơn Poisson).
    /// </summary>
    public static double[] NegativeBinomial(double mean, double dispersion, int max)
    {
        mean = Math.Max(mean, 0.01);
        if (dispersion <= 1.0001)
        {
            return Poisson(mean, max);
        }

        var r = mean / (dispersion - 1);
        var q = mean / (r + mean);
        var p = new double[max + 1];
        p[0] = Math.Pow(r / (r + mean), r);
        for (var k = 1; k <= max; k++)
        {
            p[k] = p[k - 1] * (k - 1 + r) / k * q;
        }

        return Normalize(p);
    }

    /// <summary>Phân phối hiệu số (nhà − khách) của hai biến độc lập: chỉ số = hiệu số + max khách.</summary>
    public static (double[] Diff, int Offset) Difference(double[] home, double[] away)
    {
        var offset = away.Length - 1;
        var diff = new double[home.Length + away.Length - 1];
        for (var h = 0; h < home.Length; h++)
        {
            for (var a = 0; a < away.Length; a++)
            {
                diff[h - a + offset] += home[h] * away[a];
            }
        }

        return (diff, offset);
    }

    public static double[] Sum(double[] home, double[] away)
    {
        var sum = new double[home.Length + away.Length - 1];
        for (var h = 0; h < home.Length; h++)
        {
            for (var a = 0; a < away.Length; a++)
            {
                sum[h + a] += home[h] * away[a];
            }
        }

        return sum;
    }

    /// <summary>Xác suất các kết quả của cửa Tài/Xỉu mức <paramref name="line"/> trên phân phối tổng.</summary>
    public static PickProbability Totals(double[] total, PickSide side, decimal line)
    {
        var over = side == PickSide.Over;
        return Accumulate(total.Select((p, k) => (p, Margin: (decimal)(over ? k : -k))), over ? -line : line);
    }

    /// <summary>Xác suất các kết quả của cửa chấp nhà/khách với mức nhìn từ cửa đó, trên phân phối hiệu số nhà − khách.</summary>
    public static PickProbability Handicap((double[] Diff, int Offset) diff, PickSide side, decimal lineForSide)
    {
        var home = side == PickSide.Home;
        return Accumulate(diff.Diff.Select((p, i) => (p, Margin: (decimal)(home ? i - diff.Offset : diff.Offset - i))), lineForSide);
    }

    private static PickProbability Accumulate(IEnumerable<(double P, decimal Margin)> cells, decimal line)
    {
        double win = 0, halfWin = 0, push = 0, halfLoss = 0, loss = 0;
        foreach (var (p, margin) in cells)
        {
            if (p <= 0)
            {
                continue;
            }

            switch (Settlement.SettleMargin(margin, line, 2m).Outcome)
            {
                case BetOutcome.Win: win += p; break;
                case BetOutcome.HalfWin: halfWin += p; break;
                case BetOutcome.Push: push += p; break;
                case BetOutcome.HalfLoss: halfLoss += p; break;
                default: loss += p; break;
            }
        }

        return new PickProbability(win, halfWin, push, halfLoss, loss);
    }

    private static double[] Normalize(double[] p)
    {
        var total = p.Sum();
        return total <= 0 ? p : p.Select(x => x / total).ToArray();
    }
}
