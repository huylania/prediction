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
    /// <summary>Đứng ngoài: biên không rõ ràng, bị bộ lọc sắt chặn, tỉ lệ thắng thấp hoặc EV âm.</summary>
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

    /// <summary>Tổng góc hết hiệp 1 (C_H1) — dùng ở cửa sổ H2 để tách nhịp góc riêng hiệp 2.</summary>
    public int? FirstHalfCorners { get; init; }

    public int? Shots { get; init; }
    public int? ShotsOnTarget { get; init; }
    public int? BlockedShots { get; init; }

    /// <summary>Cú sút từng đội (để biết đội bị dẫn còn lên bóng không).</summary>
    public int? HomeShots { get; init; }
    public int? AwayShots { get; init; }

    /// <summary>Kiểm soát bóng của đội nhà (%).</summary>
    public int? HomePossession { get; init; }

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

    /// <summary>Bù giờ dự kiến của hiệp (mặc định H1 2 phút → ΔT = 47 − T; H2 5 phút → ΔT = 95 − T, 6 phút khi ≥ 4 bàn).</summary>
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

    /// <summary>Độ sâu kèo chấp trước trận của cửa trên (vd 0.75) — hòa mà cửa trên chấp sâu thì buộc phải thắng.</summary>
    public decimal? PreMatchHandicap { get; init; }

    public int Corners => HomeCorners + AwayCorners;
}

public sealed record Factor(string Name, double Value, string Reason);

/// <summary>Một bộ lọc sắt của kỹ năng góc v2.0: đạt hay bị chặn.</summary>
public sealed record CornerFilter(string Name, bool Passed, string Text);

/// <summary>Kết quả theo cấu trúc mục 5 của file kỹ năng góc (v2.0).</summary>
public sealed class LiveCornerResult
{
    public required CornerWindow Window { get; init; }
    public int Minute { get; init; }
    public int Corners { get; init; }

    /// <summary>Tần suất góc cơ sở đã hiệu chỉnh (góc/phút): R_adj (H1) hoặc R_base (H2).</summary>
    public double Rate { get; init; }

    /// <summary>Tốc độ góc thô C/T.</summary>
    public double RawRate { get; init; }

    /// <summary>H2: tốc độ góc riêng hiệp 2 nếu biết số góc hiệp 1.</summary>
    public double? SecondHalfRate { get; init; }

    public IReadOnlyList<Factor> Factors { get; init; } = [];

    /// <summary>α (H1) hoặc β (H2) sau khi kẹp biên độ.</summary>
    public double Multiplier { get; init; }

    /// <summary>Tích các hệ số trước khi kẹp.</summary>
    public double RawMultiplier { get; init; }

    public double RemainingMinutes { get; init; }
    public double ExpectedRemaining { get; init; }
    public double ExpectedTotal { get; init; }

    public decimal? Line { get; init; }

    /// <summary>Khoảng cách kèo cần vượt D_line = mốc live − số góc hiện tại.</summary>
    public double? DLine { get; init; }

    public double? Edge { get; init; }

    /// <summary>H2: tỉ số đã an bài.</summary>
    public bool Settled { get; init; }

    public CornerCall Call { get; init; }

    public PickSide? Side => Call switch { CornerCall.Over => PickSide.Over, CornerCall.Under => PickSide.Under, _ => null };

    public string Headline { get; init; } = "";

    /// <summary>Xác suất Poisson các kết quả của cửa được chọn (hoặc cửa có biên lớn hơn khi đứng ngoài/theo dõi).</summary>
    public PickProbability? Probability { get; init; }

    /// <summary>Tỉ lệ thắng không tính hòa kèo: P(thắng) / (P(thắng) + P(thua)).</summary>
    public double? WinRate { get; init; }

    public PickSide ProbabilitySide { get; init; }

    public double? ExpectedValue { get; init; }

    public double Confidence { get; init; }

    public IReadOnlyList<CornerFilter> Filters { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];

    public string Timing { get; init; } = "";

    public bool InGoldenWindow { get; init; }
}

/// <summary>
/// Mô hình định lượng góc live theo file kỹ năng "phan-tich-goc" v2.0 (đã hiệu chỉnh thực chiến):
/// H1 (phút 25–30): R_adj = 0.55·C/T + 0.45·0.105; α = kẹp(α_sút × α_nhịp × α_tỉ số, 0.72, 1.25); ΔT = 47 − T.
/// H2 (phút 75–80): R_base tách nhịp riêng hiệp 2, sàn 0.068; β = kẹp(K_tỉ số × K_động lực × K_cường độ × K_thẻ đỏ, 0.58, 1.40); ΔT = 95 − T.
/// Số góc còn lại ~ Poisson(λ_rem); bộ lọc sắt theo khoảng cách kèo D_line; chấm điểm theo bậc tỉ lệ thắng (không tính hòa).
/// </summary>
public static class LiveCornerAnalyzer
{
    /// <summary>Tốc độ góc chuẩn của bóng đá (góc/phút).</summary>
    public const double NormRate = 0.105;

    /// <summary>Sàn tốc độ góc hiệp 2 (chống dự phóng 0 góc).</summary>
    public const double MinSecondHalfRate = 0.068;

    public const double DefaultStoppageH1 = 2.0;
    public const double DefaultStoppageH2 = 5.0;

    /// <summary>Tỉ lệ thắng (không tính hòa) tối thiểu để phát lệnh.</summary>
    public const double MinWinRate = 0.62;

    public const double MinEdgeH1 = 0.60;
    public const double MinEdgeH2 = 0.65;
    public const decimal MinOdds = 1.75m;
    public const decimal MinOverOddsH1 = 1.78m;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static bool IsGoldenWindow(CornerWindow window, int minute) =>
        window == CornerWindow.FirstHalf ? minute is >= 25 and <= 30 : minute is >= 75 and <= 80;

    public static LiveCornerResult Analyze(LiveCornerInput input) =>
        input.Window == CornerWindow.FirstHalf ? FirstHalf(input) : SecondHalf(input);

    // ------------------------------------------------------------------ Hiệp 1

    private static LiveCornerResult FirstHalf(LiveCornerInput i)
    {
        var t = Math.Max(1, i.Minute);
        var raw = (double)i.Corners / t;
        var rate = 0.55 * raw + 0.45 * NormRate;
        var shot = AlphaShot(i);
        var tempo = AlphaTempo(i);
        var factors = new List<Factor> { shot, tempo, AlphaScore(i, shot.Value >= 1.12 || tempo.Value >= 1.08) };
        var product = factors.Aggregate(1.0, (a, f) => a * f.Value);
        var alpha = Math.Clamp(product, 0.72, 1.25);
        var remaining = Math.Max(0.5, 45 + (i.StoppageMinutes ?? DefaultStoppageH1) - i.Minute);
        var expRemaining = rate * alpha * remaining;
        var exp = i.Corners + expRemaining;
        double? dLine = i.Line is { } l ? (double)l - i.Corners : null;
        double? edge = dLine is { } d ? expRemaining - d : null;

        var filters = new List<CornerFilter>();
        var notes = new List<string>();
        if (Math.Abs(product - alpha) > 1e-9)
        {
            notes.Add($"Tích hệ số {product.ToString("0.00", Inv)} bị kẹp về {alpha.ToString("0.00", Inv)} (chống nhân kép biến số).");
        }

        var (call, headline) = (CornerCall.NoBet, "");
        if (i.Line is not { } line || dLine is not { } dl || edge is not { } e)
        {
            headline = "Chưa có mốc góc live hiệp 1 để so.";
        }
        else if (e >= 0)
        {
            var burst = i.Corners >= 5 && (line >= 7.0m || dl >= 2.5);
            filters.Add(new CornerFilter("Cấm Tài mốc cao", !burst, burst
                ? $"Đã {i.Corners} góc và mốc {LineText.Number(line)} (cần thêm {Num(dl)}): hồi quy cuối hiệp 1 rất cao → tuyệt đối không Tài"
                : "Không rơi vào bẫy bùng nổ góc sớm"));
            filters.Add(new CornerFilter("Mốc H1 ≤ 6.5", line <= 6.5m, $"Mốc {LineText.Number(line)}"));
            filters.Add(new CornerFilter("Cần thêm ≤ 2 góc", dl <= 2.0, $"D_line = {Num(dl)}"));
            filters.Add(OddsFilter("Odds Tài ≥ 1.78", i.OverOdds, MinOverOddsH1));
            filters.Add(new CornerFilter("Biên ≥ +0.60", e >= MinEdgeH1, $"Δedge = {Signed(e)}"));
            (call, headline) = Decide(filters, e, MinEdgeH1, CornerCall.Over, $"TÀI GÓC H1 {LineText.Number(line)}", hardFilters: 3);
        }
        else
        {
            var exception = i.Minute >= 28 && i.Corners == 0 && (i.Shots ?? int.MaxValue) <= 2;
            var minD = exception ? 1.5 : 2.0;
            filters.Add(new CornerFilter("Đệm Xỉu ≥ 2 góc", dl >= minD, exception
                ? $"D_line = {Num(dl)} (ngoại lệ: phút ≥ 28, 0 góc, ≤ 2 cú sút → cho phép Xỉu 1.5)"
                : $"D_line = {Num(dl)}: Xỉu H1 cần mốc cách số góc hiện tại ≥ 2"));
            filters.Add(OddsFilter("Odds Xỉu ≥ 1.75", i.UnderOdds, MinOdds));
            filters.Add(new CornerFilter("Biên ≤ −0.60", e <= -MinEdgeH1, $"Δedge = {Signed(e)}"));
            (call, headline) = Decide(filters, -e, MinEdgeH1, CornerCall.Under, $"XỈU GÓC H1 {LineText.Number(line)}", hardFilters: 1);
        }

        return Finish(i, raw, rate, null, factors, product, alpha, remaining, expRemaining, exp, dLine, edge, false, call, headline, filters, notes);
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
            >= 9.5 when blocked >= 3 => new Factor("α_sút", 1.18, $"{text}: sút rất nhiều, nhiều bóng bị chặn"),
            >= 7 when blocked >= 2 => new Factor("α_sút", 1.12, $"{text}: sút nhiều, có bóng bị chặn"),
            >= 7 => new Factor("α_sút", 1.05, $"{text}: sút nhiều nhưng ít bị chặn"),
            <= 2.5 when blocked == 0 => new Factor("α_sút", 0.82, $"{text}: rất ít sút, không bị chặn"),
            <= 3.5 when blocked == 0 => new Factor("α_sút", 0.88, $"{text}: ít sút, không bị chặn"),
            <= 3.5 => new Factor("α_sút", 0.92, $"{text}: ít sút"),
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
                >= 1.2 => new Factor("α_nhịp", 1.15, $"{text}: dồn dập"),
                >= 1.0 => new Factor("α_nhịp", 1.08, $"{text}: dồn dập"),
                <= 0.4 => new Factor("α_nhịp", 0.82, $"{text}: giằng co giữa sân"),
                <= 0.5 => new Factor("α_nhịp", 0.88, $"{text}: chậm"),
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
            >= 0.45 => new Factor("α_nhịp", 1.08, $"{source}: cao"),
            <= 0.20 => new Factor("α_nhịp", 0.82, $"{source}: giằng co"),
            <= 0.27 => new Factor("α_nhịp", 0.88, $"{source}: chậm"),
            _ => new Factor("α_nhịp", 1.00, $"{source}: trung bình"),
        };
    }

    private static Factor AlphaScore(LiveCornerInput i, bool pressing)
    {
        var score = $"{i.HomeGoals}-{i.AwayGoals}";
        var margin = i.HomeGoals - i.AwayGoals;
        var leader = margin > 0 ? PickSide.Home : margin < 0 ? PickSide.Away : (PickSide?)null;
        if (Math.Abs(margin) >= 2 && (i.Favourite is null || leader == i.Favourite))
        {
            return new Factor("α_tỉ số", 0.82, $"{score}: {(i.Favourite is null ? "một đội" : "cửa trên")} đã dẫn ≥ 2 bàn trước phút 30 → giảm nhịp ép sân");
        }

        if (margin == 0 && i.HomeGoals == 0 && pressing)
        {
            return new Factor("α_tỉ số", 1.05, $"{score}: hòa 0-0 mà ép sân mạnh");
        }

        if (leader is not null && i.Favourite is { } fav && leader != fav)
        {
            return new Factor("α_tỉ số", 1.05, $"{score}: cửa trên đang bị dẫn → dồn ép");
        }

        return new Factor("α_tỉ số", 1.00, $"{score}: bình thường");
    }

    // ------------------------------------------------------------------ Hiệp 2

    private static LiveCornerResult SecondHalf(LiveCornerInput i)
    {
        var t = Math.Max(1, i.Minute);
        var raw = (double)i.Corners / t;
        double? h2Rate = null;
        double rate;
        if (i.FirstHalfCorners is { } h1 && h1 <= i.Corners && i.Minute > 45)
        {
            h2Rate = (double)(i.Corners - h1) / Math.Max(1, i.Minute - 45);
            rate = Math.Max(MinSecondHalfRate, 0.55 * h2Rate.Value + 0.25 * raw + 0.20 * NormRate);
        }
        else
        {
            rate = Math.Max(MinSecondHalfRate, 0.50 * raw + 0.50 * NormRate);
        }

        var margin = Math.Abs(i.HomeGoals - i.AwayGoals);
        var goals = i.HomeGoals + i.AwayGoals;
        var ratio = RecentRatio(i);
        var score = KScore(i, margin, goals, ratio, h2Rate);
        var red = KRed(i);
        var factors = new List<Factor> { score.Factor, KMoti(i), KInte(i, ratio), red.Factor };
        var product = factors.Aggregate(1.0, (a, f) => a * f.Value);
        var beta = Math.Clamp(product, 0.58, 1.40);
        var stoppage = i.StoppageMinutes ?? (goals >= 4 ? DefaultStoppageH2 + 1 : DefaultStoppageH2);
        var remaining = Math.Max(0.5, 90 + stoppage - i.Minute);
        var expRemaining = rate * beta * remaining;
        var exp = i.Corners + expRemaining;
        double? dLine = i.Line is { } l ? (double)l - i.Corners : null;
        double? edge = dLine is { } d ? expRemaining - d : null;

        var filters = new List<CornerFilter>();
        var notes = new List<string>();
        if (Math.Abs(product - beta) > 1e-9)
        {
            notes.Add($"Tích hệ số {product.ToString("0.00", Inv)} bị kẹp về {beta.ToString("0.00", Inv)} (tránh nhân chồng quá mức).");
        }

        var (call, headline) = (CornerCall.NoBet, "");
        if (i.Line is not { } line || dLine is not { } dl || edge is not { } e)
        {
            headline = "Chưa có mốc góc live cả trận để so.";
        }
        else if (e >= 0)
        {
            var banReason = score.BanOver ?? red.BanOver;
            filters.Add(new CornerFilter("Tỉ số / thẻ đỏ cho phép Tài", banReason is null, banReason ?? "Không bị cấm Tài theo tỉ số, thẻ đỏ"));
            var trap = dl <= 1.0 && i.OverOdds >= 1.95m && i.Minute <= 77;
            filters.Add(new CornerFilter("Không phải bẫy kèo rẻ", !trap, trap
                ? $"Phút {i.Minute} mà chỉ cần thêm {Num(dl)} góc với odds {LineText.Odds(i.OverOdds!.Value)}: nhà cái đọc nhịp H2 đã chết → cấm Tài"
                : "Không có dấu hiệu kèo rẻ giả tạo"));
            filters.Add(new CornerFilter("Cần thêm 1.5–2 góc", dl is >= 1.5 and <= 2.0, $"D_line = {Num(dl)}: Tài phút 75–80 chỉ khi cần thêm 1.5 hoặc 2 góc"));
            filters.Add(OddsFilter("Odds Tài ≥ 1.75", i.OverOdds, MinOdds));
            filters.Add(new CornerFilter("Biên ≥ +0.65", e >= MinEdgeH2, $"Δedge = {Signed(e)}"));
            (call, headline) = Decide(filters, e, MinEdgeH2, CornerCall.Over, $"TÀI GÓC CẢ TRẬN {LineText.Number(line)}", hardFilters: 3);
        }
        else
        {
            filters.Add(new CornerFilter("Tỉ số cho phép Xỉu", score.BanUnder is null, score.BanUnder ?? "Không bị cấm Xỉu theo tỉ số"));
            filters.Add(new CornerFilter("Đệm Xỉu ≥ 2 góc", dl >= 2.0, $"D_line = {Num(dl)}: Xỉu phút 75–80 cần mốc cách số góc hiện tại ≥ 2"));
            filters.Add(OddsFilter("Odds Xỉu ≥ 1.75", i.UnderOdds, MinOdds));
            filters.Add(new CornerFilter("Biên ≤ −0.65", e <= -MinEdgeH2, $"Δedge = {Signed(e)}"));
            (call, headline) = Decide(filters, -e, MinEdgeH2, CornerCall.Under, $"XỈU GÓC CẢ TRẬN {LineText.Number(line)}", hardFilters: 2);
        }

        return Finish(i, raw, rate, h2Rate, factors, product, beta, remaining, expRemaining, exp, dLine, edge, score.Settled, call, headline, filters, notes);
    }

    private sealed record ScoreState(Factor Factor, bool Settled, string? BanOver, string? BanUnder);

    private sealed record RedState(Factor Factor, string? BanOver);

    /// <summary>Nhịp (sút + góc)/phút của đoạn gần nhất so với trung bình trận.</summary>
    private static double? RecentRatio(LiveCornerInput i)
    {
        if (i.RecentMinutes is not (>= 8 and var rm) || i.RecentShots is not { } rs)
        {
            return null;
        }

        var overall = ((i.Shots ?? 0) + i.Corners) / (double)Math.Max(1, i.Minute);
        var recent = (rs + (i.RecentCorners ?? 0)) / (double)rm;
        return overall <= 0 ? 1 : recent / overall;
    }

    private static ScoreState KScore(LiveCornerInput i, int margin, int goals, double? ratio, double? h2Rate)
    {
        var score = $"{i.HomeGoals}-{i.AwayGoals}";
        var trailing = i.HomeGoals < i.AwayGoals ? PickSide.Home : i.AwayGoals < i.HomeGoals ? PickSide.Away : (PickSide?)null;

        if (margin >= 3)
        {
            return new ScoreState(new Factor("K_tỉ số", 0.60, $"{score}: cách {margin} bàn, đã an bài hoàn toàn"), true,
                $"{score}: cách ≥ 3 bàn → bắt buộc cấm Tài góc", null);
        }

        if (margin == 2)
        {
            var favTrailing = trailing is not null && trailing == i.Favourite;
            if (favTrailing || trailing == PickSide.Home || goals >= 4)
            {
                var k = favTrailing ? 1.25 : trailing == PickSide.Home ? 1.20 : 1.15;
                var who = favTrailing ? "cửa trên đang bị dẫn 2 bàn" : trailing == PickSide.Home ? "chủ nhà đang bị dẫn 2 bàn" : $"trận rượt đuổi {goals} bàn";
                return new ScoreState(new Factor("K_tỉ số", k, $"{score}: {who} → chưa an bài, đội bị dẫn dồn bóng tạt cánh"), false,
                    null, $"{score}: chưa an bài (B1) → cấm Xỉu góc");
            }

            var text = $"{score}: đội dẫn 2 bàn kiểm soát trận → đã an bài (B2), ưu tiên Xỉu";
            var shotsTrailing = trailing == PickSide.Home ? i.HomeShots : i.AwayShots;
            if (shotsTrailing is { } st)
            {
                text += $" (đội bị dẫn {st} cú sút cả trận{(st > 5 ? ", vẫn còn lên bóng: theo dõi" : "")})";
            }

            return new ScoreState(new Factor("K_tỉ số", 0.72, text), true, null, null);
        }

        if (margin == 1)
        {
            if (trailing == i.Favourite && i.Favourite is not null)
            {
                return new ScoreState(new Factor("K_tỉ số", 1.35, $"{score}: cửa trên đang bị dẫn 1 bàn → dồn ép mạnh nhất"), false, null, null);
            }

            return trailing == PickSide.Home
                ? new ScoreState(new Factor("K_tỉ số", 1.30, $"{score}: chủ nhà bị dẫn 1 bàn → dồn ép"), false, null, null)
                : new ScoreState(new Factor("K_tỉ số", 1.20, $"{score}: cách 1 bàn, đội bị dẫn dồn ép"), false, null, null);
        }

        if (i.DrawSuitsBoth)
        {
            return new ScoreState(new Factor("K_tỉ số", 0.85, $"{score}: hòa mà hai bên hài lòng 1 điểm"), true,
                $"{score}: hòa, hai bên hài lòng → cấm Tài góc", null);
        }

        var deepFav = i.PreMatchHandicap >= 0.75m;
        var pressing = ratio >= 1.3 || (i.DangerousAttacks is { } da && (double)da / Math.Max(1, i.Minute) >= 1.0);
        var slow = ratio <= 0.8 || h2Rate <= 0.05;
        if (deepFav || pressing)
        {
            var k = deepFav && pressing ? 1.25 : 1.18;
            var why = string.Join(", ", new[] { deepFav ? $"cửa trên chấp {LineText.Number(i.PreMatchHandicap!.Value)} buộc phải thắng" : null, pressing ? "nhịp tấn công gần nhất dồn dập" : null }.Where(x => x is not null));
            return new ScoreState(new Factor("K_tỉ số", k, $"{score}: đang hòa, {why}"), false, null, null);
        }

        if (slow)
        {
            return new ScoreState(new Factor("K_tỉ số", 0.85, $"{score}: đang hòa, nhịp H2 chậm → hai bên chấp nhận 1 điểm"), true,
                $"{score}: hòa, nhịp H2 chậm → cấm Tài góc", null);
        }

        return new ScoreState(new Factor("K_tỉ số", 1.00, $"{score}: đang hòa, chưa rõ xu hướng (không chấp sâu, nhịp bình thường)"), false, null, null);
    }

    private static Factor KMoti(LiveCornerInput i) => i.Motivation switch
    {
        Motivation.High => new Factor("K_động lực", 1.12, "cần bàn thắng sống còn (trụ hạng, knock-out, đua vô địch)"),
        Motivation.Low => new Factor("K_động lực", 0.85, "trận thủ tục / giao hữu"),
        _ => new Factor("K_động lực", 1.00, "động lực bình thường"),
    };

    private static Factor KInte(LiveCornerInput i, double? ratio)
    {
        var t = Math.Max(1, i.Minute);
        if (i.FirstHalfCorners is { } h1 && i.Minute >= 70 && i.Corners - h1 <= 1)
        {
            return new Factor("K_cường độ", 0.78, $"H2 đứng hình: chỉ {i.Corners - h1} góc từ phút 45 tới phút {i.Minute}");
        }

        if (i.DangerousAttacks is { } da)
        {
            var perMin = (double)da / t;
            var blocked = i.BlockedShots ?? 0;
            var text = $"tấn công nguy hiểm {perMin.ToString("0.00", Inv)}/phút, {blocked} sút bị chặn";
            return perMin switch
            {
                >= 1.0 when blocked > 0 => new Factor("K_cường độ", 1.15, $"{text}: ép sân liên tục"),
                <= 0.5 => new Factor("K_cường độ", 0.78, $"{text}: nhịp độ hạ"),
                _ => new Factor("K_cường độ", 1.00, $"{text}: trung bình"),
            };
        }

        if (ratio is not { } r)
        {
            return new Factor("K_cường độ", 1.00, "chưa có số liệu 15 phút gần nhất → 1.00");
        }

        var recentBlocked = i.RecentBlocked ?? 0;
        var desc = $"{i.RecentMinutes} phút gần nhất: {i.RecentShots} sút, {i.RecentCorners ?? 0} góc, {recentBlocked} bị chặn (nhịp {r.ToString("0.0", Inv)}× trung bình trận)";
        return r switch
        {
            >= 1.3 when recentBlocked >= 1 => new Factor("K_cường độ", 1.15, $"{desc}: dồn bóng, sút liên tục, có bóng bị chặn"),
            <= 0.6 => new Factor("K_cường độ", 0.78, $"{desc}: câu giờ / vỡ vụn"),
            _ => new Factor("K_cường độ", 1.00, $"{desc}: bình thường"),
        };
    }

    private static RedState KRed(LiveCornerInput i)
    {
        var diff = i.HomeRed - i.AwayRed;
        if (diff == 0)
        {
            return new RedState(new Factor("K_thẻ đỏ", 1.00, i.HomeRed == 0 ? "không có thẻ đỏ" : "thẻ đỏ cân bằng quân số"), null);
        }

        var shortHome = diff > 0;
        var leading = i.HomeGoals > i.AwayGoals ? PickSide.Home : i.AwayGoals > i.HomeGoals ? PickSide.Away : (PickSide?)null;
        var shortSide = shortHome ? PickSide.Home : PickSide.Away;
        var team = shortHome ? "đội nhà" : "đội khách";
        if (leading == shortSide)
        {
            return new RedState(new Factor("K_thẻ đỏ", 1.25, $"{team} đang dẫn nhưng bị thẻ đỏ: co cụm chịu trận 10 chống 11"), null);
        }

        if (leading is not null)
        {
            return new RedState(new Factor("K_thẻ đỏ", 0.72, $"{team} bị dẫn lại thiếu người: vỡ trận, mất sức tấn công"),
                $"{team} bị dẫn lại dính thẻ đỏ → cấm Tài góc");
        }

        return new RedState(new Factor("K_thẻ đỏ", 1.00, $"{team} thiếu người khi đang hòa (kỹ năng không quy định → 1.00)"), null);
    }

    // ------------------------------------------------------------------ Chung

    private static CornerFilter OddsFilter(string name, decimal? odds, decimal min) => odds is { } o
        ? new CornerFilter(name, o >= min, $"Odds {LineText.Odds(o)}")
        : new CornerFilter(name, false, "Chưa có odds live");

    /// <summary>
    /// Biên đủ mà dính bộ lọc cứng (các bộ lọc đầu) → đứng ngoài; chỉ thiếu odds/biên → theo dõi; đạt hết → vào lệnh (còn chờ kiểm tỉ lệ thắng, EV).
    /// </summary>
    private static (CornerCall, string) Decide(List<CornerFilter> filters, double edgeForSide, double minEdge, CornerCall call, string label, int hardFilters)
    {
        var hard = filters.Take(hardFilters).Where(f => !f.Passed).ToList();
        if (Math.Abs(edgeForSide) < 0.40)
        {
            return (CornerCall.NoBet, "ĐỨNG NGOÀI — biên lợi nhuận không rõ ràng");
        }

        if (hard.Count > 0)
        {
            return (CornerCall.NoBet, $"ĐỨNG NGOÀI — bộ lọc sắt chặn: {string.Join("; ", hard.Select(f => f.Name))}");
        }

        var soft = filters.Skip(hardFilters).Where(f => !f.Passed).ToList();
        if (soft.Count > 0)
        {
            return (CornerCall.Watch, edgeForSide < minEdge
                ? $"Nghiêng {(call == CornerCall.Over ? "Tài" : "Xỉu")} ({Signed(edgeForSide)}) nhưng biên chưa tới {minEdge.ToString("0.00", Inv)}"
                : $"Nghiêng {(call == CornerCall.Over ? "Tài" : "Xỉu")}: chờ {string.Join(", ", soft.Select(f => f.Name.ToLowerInvariant()))}");
        }

        return (call, label);
    }

    private static LiveCornerResult Finish(LiveCornerInput i, double raw, double rate, double? h2Rate, List<Factor> factors, double product, double multiplier,
        double remaining, double expRemaining, double exp, double? dLine, double? edge, bool settled, CornerCall call, string headline,
        List<CornerFilter> filters, List<string> notes)
    {
        PickProbability? probability = null;
        double? ev = null;
        double? winRate = null;
        var side = call == CornerCall.Under || call != CornerCall.Over && edge < 0 ? PickSide.Under : PickSide.Over;
        if (i.Line is { } line)
        {
            probability = Distributions.Totals(TotalDistribution(i.Corners, expRemaining), side, line);
            winRate = probability.Share;
            var odds = side == PickSide.Over ? i.OverOdds : i.UnderOdds;
            ev = odds is { } o ? probability.ExpectedValue(o) : null;
            var rateOk = winRate >= MinWinRate;
            var rateText = winRate is { } wv ? Pct(wv) : "—";
            filters.Add(new CornerFilter("Tỉ lệ thắng ≥ 62%", rateOk, $"Poisson (không tính hòa): {rateText}"));
            if (call is CornerCall.Over or CornerCall.Under)
            {
                if (!rateOk)
                {
                    notes.Add($"Tỉ lệ thắng không tính hòa {rateText} dưới 62%: không phát lệnh.");
                    (call, headline) = (CornerCall.NoBet, "ĐỨNG NGOÀI — tỉ lệ thắng Poisson dưới 62%");
                }
                else if (ev < 0)
                {
                    notes.Add($"EV âm ({Signed(ev.Value)}) với odds {LineText.Odds((side == PickSide.Over ? i.OverOdds : i.UnderOdds)!.Value)}: đứng ngoài.");
                    (call, headline) = (CornerCall.NoBet, "ĐỨNG NGOÀI — odds quá thấp (EV âm)");
                }
            }
        }

        if (i.OpenLine is { } open && i.Line is { } current && current != open)
        {
            notes.Add($"Mốc góc từ {LineText.Number(open)} (mở) → {LineText.Number(current)} (live).");
        }

        var minEdge = i.Window == CornerWindow.FirstHalf ? MinEdgeH1 : MinEdgeH2;
        var confidence = call is CornerCall.Over or CornerCall.Under && winRate is { } wr && edge is { } eg
            ? Confidence(wr, Math.Abs(eg), minEdge)
            : Math.Min(5.9, 5.0 + Math.Clamp(Math.Abs(edge ?? 0) * 1.5, 0, 0.9));
        if (confidence is null)
        {
            (call, headline) = (CornerCall.NoBet, "ĐỨNG NGOÀI — chưa đạt bậc điểm tối thiểu (62%, biên ≥ " + minEdge.ToString("0.00", Inv) + ")");
            confidence = 5.9;
        }

        return new LiveCornerResult
        {
            Window = i.Window,
            Minute = i.Minute,
            Corners = i.Corners,
            Rate = rate,
            RawRate = raw,
            SecondHalfRate = h2Rate,
            Factors = factors,
            Multiplier = multiplier,
            RawMultiplier = product,
            RemainingMinutes = remaining,
            ExpectedRemaining = expRemaining,
            ExpectedTotal = exp,
            Line = i.Line,
            DLine = dLine,
            Edge = edge,
            Settled = settled,
            Call = call,
            Headline = headline,
            Probability = probability,
            WinRate = winRate,
            ProbabilitySide = side,
            ExpectedValue = ev,
            Confidence = Math.Round(confidence.Value, 1),
            Filters = filters,
            Notes = notes,
            Timing = Timing(i, call, exp, expRemaining),
            InGoldenWindow = IsGoldenWindow(i.Window, i.Minute),
        };
    }

    /// <summary>
    /// Điểm tin cậy theo bậc của kỹ năng (không lạm phát 10/10): 9.0–9.5 (≥ 72%, |Δ| ≥ 1.10), 8.0–8.9 (65–71%, 0.75–1.09),
    /// 7.0–7.9 (62–64%, 0.65–0.74; H1 từ 0.60). Bậc là bậc thấp hơn của hai tiêu chí; dưới bậc 7 → null (đứng ngoài).
    /// </summary>
    public static double? Confidence(double winRate, double absEdge, double minEdge = MinEdgeH2)
    {
        (double Lo, double Hi, double Floor, double Width)[] bands = [(0.72, 1.10, 9.0, 0.5), (0.65, 0.75, 8.0, 0.9), (MinWinRate, minEdge, 7.0, 0.9)];
        int? ByRate() => winRate >= 0.72 ? 0 : winRate >= 0.65 ? 1 : winRate >= MinWinRate ? 2 : null;
        int? ByEdge() => absEdge >= 1.10 ? 0 : absEdge >= 0.75 ? 1 : absEdge >= minEdge ? 2 : null;
        if (ByRate() is not { } r || ByEdge() is not { } e)
        {
            return null;
        }

        var band = Math.Max(r, e);
        var (lo, edgeLo, floor, width) = bands[band];
        var (hiRate, hiEdge) = band switch { 0 => (0.80, 1.60), 1 => (0.72, 1.10), _ => (0.65, 0.75) };
        var posRate = Math.Clamp((winRate - lo) / (hiRate - lo), 0, 1);
        var posEdge = Math.Clamp((absEdge - edgeLo) / (hiEdge - edgeLo), 0, 1);
        return floor + width * Math.Min(posRate, posEdge);
    }

    /// <summary>Phân phối tổng góc cuối hiệp/trận: góc hiện có + số góc còn lại ~ Poisson(λ_rem).</summary>
    public static double[] TotalDistribution(int current, double expectedRemaining)
    {
        var remaining = Distributions.Poisson(Math.Max(0.05, expectedRemaining), 30);
        var total = new double[current + remaining.Length];
        Array.Copy(remaining, 0, total, current, remaining.Length);
        return total;
    }

    /// <summary>
    /// Mốc tốt nhất còn đạt tỉ lệ thắng ≥ 62% (không tính hòa) và đúng bộ lọc khoảng cách kèo:
    /// Tài — mốc xa nhất nhưng chỉ cần thêm ≤ 2 góc; Xỉu — mốc thấp nhất nhưng còn đệm ≥ 2 góc.
    /// </summary>
    public static decimal? BestLine(int current, double expectedRemaining, PickSide side)
    {
        var total = TotalDistribution(current, expectedRemaining);
        decimal? best = null;
        var from = side == PickSide.Over ? current + 0.5m : current + 2.0m;
        var to = side == PickSide.Over ? current + 2.0m : current + 15m;
        for (var line = from; line <= to; line += 0.5m)
        {
            if (Distributions.Totals(total, side, line).Share >= MinWinRate)
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
                var minOdds = i.Window == CornerWindow.FirstHalf ? MinOverOddsH1 : MinOdds;
                return $"Vào Tài góc {half} ngay trong cửa sổ {(i.Window == CornerWindow.FirstHalf ? "25–30'" : "75–80'")}, trước khi nhà cái nâng mốc"
                    + (max is { } m ? $"; chỉ vào khi mốc ≤ {LineText.Number(m)} (cần thêm ≤ 2 góc), odds ≥ {LineText.Odds(minOdds)}." : $"; odds ≥ {LineText.Odds(minOdds)}.");
            }

            case CornerCall.Under:
            {
                var min = BestLine(i.Corners, expRemaining, PickSide.Under);
                return $"Vào Xỉu góc {half} khi mốc live còn ≥ {(min is { } m ? LineText.Number(m) : LineText.Number(i.Line ?? 0))} (đệm ≥ 2 góc), odds ≥ 1.75; có góc ngay sau đó thì chờ mốc mới.";
            }

            case CornerCall.Watch:
                return $"Theo dõi thêm 2–3 phút: kỳ vọng {exp.ToString("0.0", Inv)} góc {half}; vào khi mốc / odds đạt bộ lọc.";
            default:
                return "Đứng ngoài, bảo toàn vốn.";
        }
    }

    private static string Num(double value) => value.ToString("0.##", Inv);

    private static string Pct(double value) => (value * 100).ToString("0.0", Inv) + "%";

    private static string Signed(double value) => (value >= 0 ? "+" : "−") + Math.Abs(value).ToString("0.00", Inv);
}
