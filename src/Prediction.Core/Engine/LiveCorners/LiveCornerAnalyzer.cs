using System.Globalization;
using Prediction.Core.Engine.Analysis;
using Prediction.Core.Entities;

namespace Prediction.Core.Engine.LiveCorners;

public enum Motivation
{
    /// <summary>Trận thủ tục, đã chắc suất.</summary>
    Low = -1,

    Normal = 0,

    /// <summary>Cần điểm sống còn: trụ hạng, tranh vô địch, knock-out.</summary>
    High = 1,
}

public enum CornerCall
{
    /// <summary>Đứng ngoài: biên không rõ ràng hoặc EV âm.</summary>
    NoBet = 0,

    /// <summary>Có độ lệch nhưng chưa đủ điều kiện: theo dõi thêm.</summary>
    Watch = 1,

    Over = 2,
    Under = 3,
}

/// <summary>Số liệu live của một trận tại phút T (tự động từ API hoặc đọc từ ảnh AiScore / nhập tay).</summary>
public sealed class LiveCornerInput
{
    public required CornerWindow Window { get; init; }

    /// <summary>Phút đang đá.</summary>
    public required int Minute { get; init; }

    public int HomeCorners { get; init; }
    public int AwayCorners { get; init; }

    public int? Shots { get; init; }
    public int? ShotsOnTarget { get; init; }
    public int? BlockedShots { get; init; }

    /// <summary>Tổng tấn công nguy hiểm hai đội (AiScore). Null nếu không có — dùng chỉ số thay thế.</summary>
    public int? DangerousAttacks { get; init; }

    public int HomeGoals { get; init; }
    public int AwayGoals { get; init; }
    public int HomeRed { get; init; }
    public int AwayRed { get; init; }

    /// <summary>Mốc tài xỉu góc live của hiệp 1 (cửa sổ H1) hoặc cả trận (cửa sổ H2).</summary>
    public decimal? Line { get; init; }
    public decimal? OverOdds { get; init; }
    public decimal? UnderOdds { get; init; }
    public decimal? OpenLine { get; init; }

    /// <summary>Bù giờ dự kiến của hiệp (mặc định H1 2.5, H2 5.5 phút).</summary>
    public double? StoppageMinutes { get; init; }

    /// <summary>Nhịp độ gần nhất: số phút của đoạn, số cú sút, góc và sút bị chặn trong đoạn đó.</summary>
    public int? RecentMinutes { get; init; }
    public int? RecentShots { get; init; }
    public int? RecentCorners { get; init; }
    public int? RecentBlocked { get; init; }

    public Motivation Motivation { get; init; }

    /// <summary>Đang hòa và cả hai đội đều hài lòng với 1 điểm (coi như đã an bài).</summary>
    public bool DrawSuitsBoth { get; init; }

    /// <summary>Đội cửa trên trước trận (Home/Away) nếu biết — cửa trên bị dẫn thì dồn ép mạnh hơn.</summary>
    public PickSide? Favourite { get; init; }

    public int Corners => HomeCorners + AwayCorners;
}

public sealed record Factor(string Name, double Value, string Reason);

/// <summary>Kết quả theo cấu trúc mục 5 của file kỹ năng góc.</summary>
public sealed class LiveCornerResult
{
    public required CornerWindow Window { get; init; }
    public int Minute { get; init; }
    public int Corners { get; init; }

    /// <summary>Tần suất góc cơ sở (góc/phút).</summary>
    public double Rate { get; init; }

    public IReadOnlyList<Factor> Factors { get; init; } = [];

    /// <summary>α (H1) hoặc β (H2).</summary>
    public double Multiplier { get; init; }

    public double RemainingMinutes { get; init; }
    public double ExpectedRemaining { get; init; }
    public double ExpectedTotal { get; init; }

    public decimal? Line { get; init; }
    public double? Edge { get; init; }

    /// <summary>H2: tỉ số đã an bài (cách ≥ 2 bàn, hoặc hòa mà hai bên hài lòng).</summary>
    public bool Settled { get; init; }

    public CornerCall Call { get; init; }

    public PickSide? Side => Call switch { CornerCall.Over => PickSide.Over, CornerCall.Under => PickSide.Under, _ => null };

    public string Headline { get; init; } = "";

    /// <summary>Xác suất các kết quả của cửa được chọn (hoặc cửa có biên lớn hơn khi đứng ngoài/theo dõi).</summary>
    public PickProbability? Probability { get; init; }

    public PickSide ProbabilitySide { get; init; }

    public double? ExpectedValue { get; init; }

    public double Confidence { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = [];

    public string Timing { get; init; } = "";

    public bool InGoldenWindow { get; init; }
}

/// <summary>
/// Mô hình định lượng góc live theo file kỹ năng "phan-tich-goc":
/// H1 (phút 25–30): Exp = C + (C/T) × α_shot × α_tempo × ΔT;
/// H2 (phút 75–80): Exp = C + (C/T) × K_score × K_moti × K_inte × K_red × ΔT.
/// </summary>
public static class LiveCornerAnalyzer
{
    public const double DefaultStoppageH1 = 2.5;
    public const double DefaultStoppageH2 = 5.5;
    public const double Dispersion = 1.3;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static bool IsGoldenWindow(CornerWindow window, int minute) =>
        window == CornerWindow.FirstHalf ? minute is >= 25 and <= 30 : minute is >= 75 and <= 80;

    public static LiveCornerResult Analyze(LiveCornerInput input) =>
        input.Window == CornerWindow.FirstHalf ? FirstHalf(input) : SecondHalf(input);

    // ------------------------------------------------------------------ Hiệp 1

    private static LiveCornerResult FirstHalf(LiveCornerInput i)
    {
        var t = Math.Max(1, i.Minute);
        var rate = (double)i.Corners / t;
        var factors = new List<Factor> { AlphaShot(i), AlphaTempo(i) };
        var alpha = factors.Aggregate(1.0, (a, f) => a * f.Value);
        var remaining = Math.Max(0.5, 45 + (i.StoppageMinutes ?? DefaultStoppageH1) - i.Minute);
        var expRemaining = rate * alpha * remaining;
        var exp = i.Corners + expRemaining;
        double? edge = i.Line is { } l ? exp - (double)l : null;

        var notes = new List<string>();
        var call = CornerCall.NoBet;
        string headline;
        var shotFactor = factors[0].Value;
        if (edge is null)
        {
            headline = "Chưa có mốc góc live hiệp 1 để so.";
        }
        else if (edge >= 0.60)
        {
            if (shotFactor >= 1.0)
            {
                call = CornerCall.Over;
                headline = $"TÀI GÓC H1 {LineText.Number(i.Line!.Value)}";
            }
            else
            {
                call = CornerCall.Watch;
                headline = "Biên Tài đủ nhưng cú sút thưa — theo dõi thêm";
                notes.Add("Tài góc H1 cần cú sút dồn dập, có sút bị chặn; hiện α_sút < 1.");
            }
        }
        else if (edge <= -0.60)
        {
            if (shotFactor <= 1.0)
            {
                call = CornerCall.Under;
                headline = $"XỈU GÓC H1 {LineText.Number(i.Line!.Value)}";
            }
            else
            {
                call = CornerCall.Watch;
                headline = "Biên Xỉu đủ nhưng sút nhiều — theo dõi thêm";
                notes.Add("Xỉu góc H1 cần ít sút, nhịp độ chậm; hiện α_sút > 1.");
            }
        }
        else if (Math.Abs(edge.Value) < 0.40)
        {
            headline = "ĐỨNG NGOÀI — biên lợi nhuận không rõ ràng";
        }
        else
        {
            call = CornerCall.Watch;
            headline = $"Biên mỏng ({Signed(edge.Value)}): chờ mốc tốt hơn";
        }

        return Finish(i, rate, factors, alpha, remaining, expRemaining, exp, edge, false, call, headline, notes);
    }

    private static Factor AlphaShot(LiveCornerInput i)
    {
        if (i.Shots is not { } shots)
        {
            return new Factor("α_sút", 1.0, "chưa có số cú sút → 1.00");
        }

        // Ngưỡng trong file tính ở phút 25–30: quy đổi số cú sút về phút 28.
        var scale = 28.0 / Math.Clamp(i.Minute, 15, 45);
        var s = shots * scale;
        var blocked = i.BlockedShots ?? 0;
        var text = $"{shots} cú sút, {blocked} bị chặn" + (Math.Abs(scale - 1) > 0.15 ? $" (≈ {s:0.#} quy về phút 28)" : "");
        return s switch
        {
            >= 8 when blocked >= 2 => new Factor("α_sút", 1.25, $"{text}: sút dồn dập, có bóng bị chặn"),
            >= 6 when blocked >= 2 => new Factor("α_sút", 1.15, $"{text}: sút nhiều, có bóng bị chặn"),
            >= 6 => new Factor("α_sút", 1.05, $"{text}: sút nhiều nhưng ít bị chặn"),
            <= 2.5 when blocked == 0 => new Factor("α_sút", 0.80, $"{text}: rất ít sút, không bị chặn"),
            <= 3.5 when blocked == 0 => new Factor("α_sút", 0.85, $"{text}: ít sút, không bị chặn"),
            <= 3.5 => new Factor("α_sút", 0.90, $"{text}: ít sút"),
            _ => new Factor("α_sút", 1.00, $"{text}: mức trung bình"),
        };
    }

    private static Factor AlphaTempo(LiveCornerInput i)
    {
        var t = Math.Max(1, i.Minute);
        if (i.DangerousAttacks is { } da)
        {
            var perMin = (double)da / t;
            var text = $"tấn công nguy hiểm {perMin.ToString("0.00", Inv)}/phút";
            return perMin switch
            {
                >= 1.0 => new Factor("α_nhịp", 1.20, $"{text}: dồn dập"),
                >= 0.9 => new Factor("α_nhịp", 1.10, $"{text}: cao"),
                <= 0.4 => new Factor("α_nhịp", 0.80, $"{text}: giằng co giữa sân"),
                <= 0.5 => new Factor("α_nhịp", 0.85, $"{text}: chậm"),
                _ => new Factor("α_nhịp", 1.00, $"{text}: trung bình"),
            };
        }

        // Không có tấn công nguy hiểm (API-Football): dùng (sút + góc)/phút, trộn với 10 phút gần nhất nếu có.
        var overall = ((i.Shots ?? 0) + i.Corners) / (double)t;
        var actions = overall;
        var source = $"(sút + góc)/phút = {overall.ToString("0.00", Inv)}";
        if (i.RecentMinutes is >= 8 and var rm && i.RecentShots is { } rs)
        {
            var recent = (rs + (i.RecentCorners ?? 0)) / (double)rm;
            actions = (overall + recent) / 2;
            source += $", {rm} phút gần nhất {recent.ToString("0.00", Inv)}";
        }

        source = "chỉ số thay thế " + source;
        return actions switch
        {
            >= 0.55 => new Factor("α_nhịp", 1.15, $"{source}: dồn dập"),
            >= 0.45 => new Factor("α_nhịp", 1.10, $"{source}: cao"),
            <= 0.20 => new Factor("α_nhịp", 0.80, $"{source}: giằng co"),
            <= 0.27 => new Factor("α_nhịp", 0.85, $"{source}: chậm"),
            _ => new Factor("α_nhịp", 1.00, $"{source}: trung bình"),
        };
    }

    // ------------------------------------------------------------------ Hiệp 2

    private static LiveCornerResult SecondHalf(LiveCornerInput i)
    {
        var t = Math.Max(1, i.Minute);
        var rate = (double)i.Corners / t;
        var margin = Math.Abs(i.HomeGoals - i.AwayGoals);
        var settled = margin >= 2 || margin == 0 && i.DrawSuitsBoth;
        var factors = new List<Factor> { KScore(i, margin), KMoti(i), KInte(i), KRed(i) };
        var beta = factors.Aggregate(1.0, (a, f) => a * f.Value);
        var remaining = Math.Max(0.5, 90 + (i.StoppageMinutes ?? DefaultStoppageH2) - i.Minute);
        var expRemaining = rate * beta * remaining;
        var exp = i.Corners + expRemaining;
        double? edge = i.Line is { } l ? exp - (double)l : null;

        var notes = new List<string>();
        var call = CornerCall.NoBet;
        string headline;
        if (edge is null)
        {
            headline = "Chưa có mốc góc live cả trận để so.";
        }
        else if (Math.Abs(edge.Value) < 0.40)
        {
            headline = "ĐỨNG NGOÀI — biên lợi nhuận không rõ ràng";
        }
        else if (edge >= 0.55 && !settled && beta >= 1.20)
        {
            call = CornerCall.Over;
            headline = $"TÀI GÓC CẢ TRẬN {LineText.Number(i.Line!.Value)}";
        }
        else if (edge <= -0.55 && settled && beta <= 0.80)
        {
            call = CornerCall.Under;
            headline = $"XỈU GÓC CẢ TRẬN {LineText.Number(i.Line!.Value)}";
        }
        else
        {
            call = CornerCall.Watch;
            headline = edge > 0 ? $"Nghiêng Tài ({Signed(edge.Value)}) nhưng chưa đủ điều kiện" : $"Nghiêng Xỉu ({Signed(edge.Value)}) nhưng chưa đủ điều kiện";
            if (edge > 0 && settled)
            {
                notes.Add("Tài góc H2 cần tỉ số chưa an bài.");
            }
            else if (edge > 0 && beta < 1.20)
            {
                notes.Add($"Tài góc H2 cần β ≥ 1.20 (hiện {beta.ToString("0.00", Inv)}).");
            }
            else if (edge < 0 && !settled)
            {
                notes.Add("Xỉu góc H2 cần tỉ số đã an bài (trận chưa ngã ngũ dễ có chuỗi góc cuối trận).");
            }
            else if (edge < 0 && beta > 0.80)
            {
                notes.Add($"Xỉu góc H2 cần β ≤ 0.80 (hiện {beta.ToString("0.00", Inv)}).");
            }

            if (Math.Abs(edge.Value) < 0.55)
            {
                notes.Add("Biên chưa tới ±0.55.");
            }
        }

        return Finish(i, rate, factors, beta, remaining, expRemaining, exp, edge, settled, call, headline, notes);
    }

    private static Factor KScore(LiveCornerInput i, int margin)
    {
        var score = $"{i.HomeGoals}-{i.AwayGoals}";
        if (margin == 0 && i.DrawSuitsBoth)
        {
            return new Factor("K_tỉ số", 0.75, $"{score}, hòa mà hai bên hài lòng: coi như an bài");
        }

        if (margin <= 1)
        {
            var trailing = i.HomeGoals < i.AwayGoals ? PickSide.Home : i.AwayGoals < i.HomeGoals ? PickSide.Away : (PickSide?)null;
            return trailing is not null && trailing == i.Favourite
                ? new Factor("K_tỉ số", 1.40, $"{score}: chưa an bài, cửa trên đang bị dẫn → dồn ép tạt bóng")
                : new Factor("K_tỉ số", 1.30, $"{score}: chưa an bài (hòa hoặc cách 1 bàn)");
        }

        return margin >= 3
            ? new Factor("K_tỉ số", 0.65, $"{score}: đã an bài (cách {margin} bàn)")
            : new Factor("K_tỉ số", 0.70, $"{score}: đã an bài (cách 2 bàn)");
    }

    private static Factor KMoti(LiveCornerInput i) => i.Motivation switch
    {
        Motivation.High => new Factor("K_động lực", 1.20, "cần điểm sống còn (trụ hạng, tranh cúp, knock-out)"),
        Motivation.Low => new Factor("K_động lực", 0.85, "trận vô thưởng vô phạt"),
        _ => new Factor("K_động lực", 1.00, "động lực bình thường"),
    };

    private static Factor KInte(LiveCornerInput i)
    {
        var t = Math.Max(1, i.Minute);
        if (i.DangerousAttacks is { } da)
        {
            var perMin = (double)da / t;
            var blocked = i.BlockedShots ?? 0;
            var text = $"tấn công nguy hiểm {perMin.ToString("0.00", Inv)}/phút, {blocked} sút bị chặn";
            return perMin switch
            {
                >= 1.0 when blocked > 0 => new Factor("K_cường độ", 1.20, $"{text}: ép sân liên tục"),
                <= 0.5 => new Factor("K_cường độ", 0.80, $"{text}: nhịp độ hạ"),
                _ => new Factor("K_cường độ", 1.00, $"{text}: trung bình"),
            };
        }

        if (i.RecentMinutes is not (>= 8 and var rm) || i.RecentShots is not { } rs)
        {
            return new Factor("K_cường độ", 1.00, "chưa có số liệu 15 phút gần nhất → 1.00");
        }

        var overall = ((i.Shots ?? 0) + i.Corners) / (double)t;
        var recent = (rs + (i.RecentCorners ?? 0)) / (double)rm;
        var ratio = overall <= 0 ? 1 : recent / overall;
        var recentBlocked = i.RecentBlocked ?? 0;
        var desc = $"{rm} phút gần nhất: {rs} sút, {i.RecentCorners ?? 0} góc, {recentBlocked} bị chặn (nhịp {ratio.ToString("0.0", Inv)}× trung bình trận)";
        return ratio switch
        {
            >= 1.6 when recentBlocked >= 1 => new Factor("K_cường độ", 1.30, $"{desc}: dồn ép rất mạnh"),
            >= 1.3 when recentBlocked >= 1 => new Factor("K_cường độ", 1.20, $"{desc}: dồn ép"),
            >= 1.15 => new Factor("K_cường độ", 1.15, $"{desc}: tăng tốc"),
            <= 0.6 => new Factor("K_cường độ", 0.75, $"{desc}: câu giờ / đi bộ"),
            <= 0.8 => new Factor("K_cường độ", 0.85, $"{desc}: hạ nhịp"),
            _ => new Factor("K_cường độ", 1.00, $"{desc}: bình thường"),
        };
    }

    private static Factor KRed(LiveCornerInput i)
    {
        var diff = i.HomeRed - i.AwayRed;
        if (diff == 0)
        {
            return new Factor("K_thẻ đỏ", 1.00, i.HomeRed == 0 ? "không có thẻ đỏ" : "thẻ đỏ cân bằng quân số");
        }

        var shortHome = diff > 0;
        var leading = i.HomeGoals > i.AwayGoals ? PickSide.Home : i.AwayGoals > i.HomeGoals ? PickSide.Away : (PickSide?)null;
        var shortSide = shortHome ? PickSide.Home : PickSide.Away;
        var team = shortHome ? "đội nhà" : "đội khách";
        if (leading == shortSide)
        {
            return new Factor("K_thẻ đỏ", 1.30, $"{team} đang dẫn nhưng bị thẻ đỏ: đối thủ ép sân hơn người");
        }

        if (leading is not null)
        {
            return new Factor("K_thẻ đỏ", 0.75, $"{team} bị dẫn lại thiếu người: mất khả năng tấn công biên");
        }

        return new Factor("K_thẻ đỏ", 1.15, $"{team} thiếu người khi đang hòa: đối thủ dâng cao");
    }

    // ------------------------------------------------------------------ Chung

    private static LiveCornerResult Finish(LiveCornerInput i, double rate, List<Factor> factors, double multiplier, double remaining,
        double expRemaining, double exp, double? edge, bool settled, CornerCall call, string headline, List<string> notes)
    {
        PickProbability? probability = null;
        double? ev = null;
        var side = call == CornerCall.Under || call != CornerCall.Over && edge < 0 ? PickSide.Under : PickSide.Over;
        if (i.Line is { } line)
        {
            var total = TotalDistribution(i.Corners, expRemaining);
            probability = Distributions.Totals(total, side, line);
            var odds = side == PickSide.Over ? i.OverOdds : i.UnderOdds;
            if (odds is { } o)
            {
                ev = probability.ExpectedValue(o);
                if (call is CornerCall.Over or CornerCall.Under && ev < 0)
                {
                    notes.Add($"EV âm ({Signed(ev.Value)}) với odds {LineText.Odds(o)}: đứng ngoài.");
                    call = CornerCall.NoBet;
                    headline = "ĐỨNG NGOÀI — odds quá thấp (EV âm)";
                }
            }
        }

        if (i.OpenLine is { } open && i.Line is { } current && current != open)
        {
            notes.Add($"Mốc góc từ {LineText.Number(open)} (mở) → {LineText.Number(current)} (live).");
        }

        var confidence = 5.0;
        if (edge is { } e)
        {
            confidence += Math.Clamp(Math.Abs(e) * 2.5, 0, 2.5);
        }

        if (probability is not null)
        {
            confidence += Math.Clamp((probability.WinAny - 0.5) * 8, -2, 2);
        }

        if (ev is { } evValue)
        {
            confidence += Math.Clamp(evValue * 5, -1, 1);
        }

        if (call is CornerCall.NoBet or CornerCall.Watch)
        {
            confidence = Math.Min(confidence, 5.9);
        }

        return new LiveCornerResult
        {
            Window = i.Window,
            Minute = i.Minute,
            Corners = i.Corners,
            Rate = rate,
            Factors = factors,
            Multiplier = multiplier,
            RemainingMinutes = remaining,
            ExpectedRemaining = expRemaining,
            ExpectedTotal = exp,
            Line = i.Line,
            Edge = edge,
            Settled = settled,
            Call = call,
            Headline = headline,
            Probability = probability,
            ProbabilitySide = side,
            ExpectedValue = ev,
            Confidence = Math.Round(Math.Clamp(confidence, 0, 10), 1),
            Notes = notes,
            Timing = Timing(i, call, exp, expRemaining),
            InGoldenWindow = IsGoldenWindow(i.Window, i.Minute),
        };
    }

    /// <summary>Phân phối tổng góc cuối hiệp/trận: góc hiện có + số góc còn lại (nhị thức âm).</summary>
    public static double[] TotalDistribution(int current, double expectedRemaining)
    {
        var remaining = Distributions.NegativeBinomial(Math.Max(0.05, expectedRemaining), Dispersion, 30);
        var total = new double[current + remaining.Length];
        Array.Copy(remaining, 0, total, current, remaining.Length);
        return total;
    }

    /// <summary>Mốc xa nhất còn có xác suất thắng ≥ 60% cho cửa đã chọn.</summary>
    public static decimal? BestLine(int current, double expectedRemaining, PickSide side)
    {
        var total = TotalDistribution(current, expectedRemaining);
        decimal? best = null;
        for (var line = current + 0.5m; line <= current + 15m; line += 0.5m)
        {
            var p = Distributions.Totals(total, side, line);
            if (p.Win + p.HalfWin / 2 >= 0.60)
            {
                if (side == PickSide.Over)
                {
                    best = line;
                }
                else if (best is null)
                {
                    best = line;
                }
            }
        }

        return best;
    }

    private static string Timing(LiveCornerInput i, CornerCall call, double exp, double expRemaining)
    {
        var half = i.Window == CornerWindow.FirstHalf ? "hiệp 1" : "cả trận";
        switch (call)
        {
            case CornerCall.Over:
            {
                var max = BestLine(i.Corners, expRemaining, PickSide.Over);
                return $"Vào Tài góc {half} ngay trong cửa sổ {(i.Window == CornerWindow.FirstHalf ? "25–30'" : "75–80'")}, trước khi nhà cái nâng mốc"
                    + (max is { } m ? $"; chỉ vào khi mốc ≤ {LineText.Number(m)} (odds ≥ 1.75)." : ".");
            }

            case CornerCall.Under:
            {
                var min = BestLine(i.Corners, expRemaining, PickSide.Under);
                return $"Vào Xỉu góc {half} khi mốc live còn ≥ {(min is { } m ? LineText.Number(m) : LineText.Number(i.Line ?? 0))} (odds ≥ 1.80); có góc ngay sau đó thì chờ mốc mới.";
            }

            case CornerCall.Watch:
                return $"Theo dõi thêm 2–3 phút: kỳ vọng {exp.ToString("0.0", Inv)} góc {half}; vào khi mốc live dịch về phía có lợi hoặc nhịp độ xác nhận.";
            default:
                return "Đứng ngoài, bảo toàn vốn.";
        }
    }

    private static string Signed(double value) => (value >= 0 ? "+" : "−") + Math.Abs(value).ToString("0.00", Inv);
}
