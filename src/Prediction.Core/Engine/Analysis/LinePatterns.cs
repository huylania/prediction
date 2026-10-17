using System.Globalization;
using Prediction.Core.Entities;

namespace Prediction.Core.Engine.Analysis;

/// <summary>Một chỉ số trong bộ chỉ số: cộng/trừ bao nhiêu điểm và vì sao.</summary>
public sealed record PatternIndicator(string Name, double Points, string Text);

public enum EntryTiming
{
    /// <summary>Không vào.</summary>
    Skip = 0,

    /// <summary>Vào được ngay.</summary>
    Now = 1,

    /// <summary>Vào trong khung 60–30 phút trước giờ bóng lăn.</summary>
    Window = 2,

    /// <summary>Kèo đang dịch ngược: chờ đội hình rồi xem lại.</summary>
    WaitLineups = 3,
}

/// <summary>Gợi ý giờ vào kèo: lúc nào vào, trong khung giờ nào và giá tối thiểu.</summary>
public sealed record EntryAdvice(EntryTiming Timing, string Text, DateTime? FromUtc, DateTime? ToUtc);

/// <summary>Một trận khớp mẫu kèo (Tài 3.0, chấp 0.75 / 1.0 cửa trên) kèm điểm, chỉ số và gợi ý giờ vào.</summary>
public sealed class LinePatternResult
{
    public required LinePattern Pattern { get; init; }

    public required PickSide Side { get; init; }

    /// <summary>Mức kèo nhìn từ cửa được chọn (Tài: 3; cửa trên: −0.75 / −1).</summary>
    public required decimal Line { get; init; }

    public required decimal Odds { get; init; }

    public required string Label { get; init; }

    public required PickProbability ModelProbability { get; init; }

    /// <summary>Xác suất đã trộn với giá nhà cái (dùng để chấm điểm).</summary>
    public required PickProbability Probability { get; init; }

    /// <summary>Phần của cửa này theo giá nhà cái (đã bỏ lời).</summary>
    public double? MarketShare { get; init; }

    public double ExpectedValue => Probability.ExpectedValue(Odds);

    /// <summary>Odds thấp nhất còn đáng vào (EV = 0) theo xác suất đã trộn.</summary>
    public decimal? MinOdds { get; init; }

    public required double Score { get; init; }

    public AnalysisGroup Group => LinePatternScorer.GroupOf(Score);

    public IReadOnlyList<PatternIndicator> Indicators { get; init; } = [];

    public required EntryAdvice Entry { get; init; }

    /// <summary>Kèo lúc mở và hiện tại (mức nhìn từ cửa được chọn) để xem biến động.</summary>
    public decimal? OpenLine { get; init; }

    public decimal? OpenOdds { get; init; }

    public DateTime? OddsUpdatedUtc { get; init; }

    public Lean Lean { get; init; }

    public int Sample { get; init; }

    /// <summary>So với giá Pinnacle cùng mức (nếu có kèo Pinnacle).</summary>
    public SharpComparison? Sharp { get; init; }
}

/// <summary>
/// Bộ chỉ số "giờ + điểm" cho các mẫu kèo người chơi thắng nhiều nhất (phân tích 91 phiếu cược):
/// Tài 3.0 (13 thắng, 8 hoàn, 4 thua), chấp 1.0 cửa trên (9–6–2) và chấp 0.75 cửa trên (2–0–2).
/// Điểm = 5 + tổng chỉ số (0–10): ≥ 8 vào được, 6–7.9 cân nhắc, &lt; 6 bỏ qua.
/// </summary>
public static class LinePatternScorer
{
    public const double EnterScore = 8.0;
    public const double ConsiderScore = 6.0;

    /// <summary>Khung giờ vào mặc định: 60–30 phút trước giờ bóng lăn (sau khi có đội hình; trung vị người chơi 61 phút).</summary>
    public static readonly TimeSpan WindowStart = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan WindowEnd = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan LastCall = TimeSpan.FromMinutes(10);

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Name(LinePattern pattern) => pattern switch
    {
        LinePattern.Over3 => "Tài 3.0",
        LinePattern.Favourite075 => "Chấp 0.75 cửa trên",
        _ => "Chấp 1.0 cửa trên",
    };

    public static AnalysisGroup GroupOf(double score) => score >= EnterScore ? AnalysisGroup.A : score >= ConsiderScore ? AnalysisGroup.B : AnalysisGroup.C;

    public static string Verdict(double score) => score >= EnterScore ? "Vào được" : score >= ConsiderScore ? "Cân nhắc" : "Bỏ qua";

    /// <summary>Các mẫu khớp với kèo hiện tại của trận.</summary>
    public static IReadOnlyList<LinePattern> Detect(AnalysisInput input)
    {
        var list = new List<LinePattern>();
        if (input.Totals is { } t && t.Current.Line == 3.0m)
        {
            list.Add(LinePattern.Over3);
        }

        if (input.Handicap is { } h)
        {
            switch (Math.Abs(h.Current.Line))
            {
                case 0.75m:
                    list.Add(LinePattern.Favourite075);
                    break;
                case 1.0m:
                    list.Add(LinePattern.Favourite1);
                    break;
            }
        }

        return list;
    }

    /// <summary>Chấm mọi mẫu khớp của trận.</summary>
    public static IReadOnlyList<LinePatternResult> Score(AnalysisInput input, MatchAnalysis analysis, DateTime nowUtc, bool lineupsKnown) =>
        Detect(input).Select(p => Score(input, analysis, p, nowUtc, lineupsKnown)).ToList();

    public static LinePatternResult Score(AnalysisInput input, MatchAnalysis analysis, LinePattern pattern, DateTime nowUtc, bool lineupsKnown)
    {
        var homeGoals = Distributions.Poisson(analysis.LambdaHome, Distributions.MaxGoals);
        var awayGoals = Distributions.Poisson(analysis.LambdaAway, Distributions.MaxGoals);
        var sample = Math.Min(analysis.HomeForm.Matches, analysis.AwayForm.Matches);

        Market market;
        PickSide side;
        decimal line, odds, otherOdds;
        decimal? openLine, openOdds;
        MarketMovement movement;
        PickProbability model;
        string label;
        if (pattern == LinePattern.Over3)
        {
            movement = input.Totals ?? throw new InvalidOperationException("Trận chưa có kèo tài xỉu.");
            market = Market.OverUnder;
            side = PickSide.Over;
            line = movement.Current.Line;
            (odds, otherOdds) = (movement.Current.PriceA, movement.Current.PriceB);
            (openLine, openOdds) = (movement.Open.Line, movement.Open.PriceA);
            model = Distributions.Totals(Distributions.Sum(homeGoals, awayGoals), PickSide.Over, line);
            label = $"Tài {LineText.Number(line)}";
        }
        else
        {
            movement = input.Handicap ?? throw new InvalidOperationException("Trận chưa có kèo chấp.");
            market = Market.AsianHandicap;
            var homeLine = movement.Current.Line;
            var home = homeLine < 0;
            side = home ? PickSide.Home : PickSide.Away;
            line = home ? homeLine : -homeLine;
            (odds, otherOdds) = home ? (movement.Current.PriceA, movement.Current.PriceB) : (movement.Current.PriceB, movement.Current.PriceA);
            (openLine, openOdds) = home ? (movement.Open.Line, movement.Open.PriceA) : (-movement.Open.Line, movement.Open.PriceB);
            model = Distributions.Handicap(Distributions.Difference(homeGoals, awayGoals), side, line);
            label = $"{(home ? input.HomeName : input.AwayName)} {LineText.Signed(line)}";
        }

        var share = PickProbability.NoVig(odds, otherOdds);
        var p = MatchAnalyzer.BlendWithMarket(model, share, sample);
        var ev = p.ExpectedValue(odds);
        decimal? minOdds = p.Share is { } s && s > 0.01 ? Math.Round((decimal)(1 / s), 2) : null;
        var lean = movement.LeanFor(side);

        var sharp = SharpPricing.Compare(input.SharpFor(market), side, line, odds);
        var indicators = new List<PatternIndicator>
        {
            new("Xác suất", Math.Clamp(6 * p.Net, -2.5, 2.5), ProbabilityText(p, model, share)),
            new("EV", Math.Clamp(8 * ev, -1.5, 1.5), $"EV {Signed(ev)} @{LineText.Odds(odds)}" + (minOdds is { } mo ? $" · giá tối thiểu {LineText.Odds(mo)}" : "")),
            Movement(movement, side, lean, openLine, openOdds, line, odds),
            WebsitePick(analysis, market, side),
            SharpPrice(sharp, side, line, odds),
        };

        if (pattern == LinePattern.Over3)
        {
            indicators.Add(ExpectedGoals(analysis));
            indicators.Add(OverForm(analysis));
            indicators.Add(OverDna(input));
        }
        else
        {
            var favHome = side == PickSide.Home;
            indicators.Add(favHome
                ? new PatternIndicator("Sân", 0.5, $"Cửa trên {input.HomeName} đá sân nhà")
                : new PatternIndicator("Sân", -0.5, $"Cửa trên {input.AwayName} đá sân khách (người chơi chỉ thắng 44% kiểu này)"));
            indicators.Add(Projection(analysis, favHome));
            indicators.Add(FavouriteDna(input, favHome));
            indicators.Add(Staffing(input, analysis, favHome));
        }

        var flags = analysis.Flags.Where(f => f.Covers(market, side)).ToList();
        if (flags.Count > 0)
        {
            indicators.Add(new PatternIndicator("Cờ đỏ", -2.0, RedFlagText.Describe(flags.Select(f => f.Number)) + " → tối đa 5.9"));
        }

        var sampleIndicator = sample < MatchAnalyzer.MinSample
            ? new PatternIndicator("Mẫu dữ liệu", -1.0, $"Chỉ {sample} trận: kém tin cậy, tối đa 7.9")
            : sample < 8
                ? new PatternIndicator("Mẫu dữ liệu", -0.5, $"Mẫu còn ngắn ({sample} trận)")
                : new PatternIndicator("Mẫu dữ liệu", 0, $"Đủ mẫu ({sample} trận)");
        indicators.Add(sampleIndicator);

        var score = Math.Round(Math.Clamp(5 + indicators.Sum(i => i.Points), 0, 10), 1);
        if (sample < MatchAnalyzer.MinSample)
        {
            score = Math.Min(score, 7.9);
        }

        if (flags.Count > 0 || ev < 0)
        {
            score = Math.Min(score, 5.9);
        }

        return new LinePatternResult
        {
            Pattern = pattern,
            Side = side,
            Line = line,
            Odds = odds,
            Label = label,
            ModelProbability = model,
            Probability = p,
            MarketShare = share,
            MinOdds = minOdds,
            Score = score,
            Indicators = indicators,
            Entry = Entry(pattern, score, lean, lineupsKnown, input.Fixture.KickoffUtc, nowUtc, line, minOdds),
            OpenLine = openLine,
            OpenOdds = openOdds,
            OddsUpdatedUtc = movement.Current.AtUtc,
            Lean = lean,
            Sample = sample,
            Sharp = sharp,
        };
    }

    /// <summary>
    /// Giá Pinnacle: Bet365 trả cao hơn giá hợp lý của Pinnacle cùng mức ≥ 3% → +1, ≥ 1.5% → +0.5 (thấp hơn thì trừ tương tự);
    /// mức chính Pinnacle dịch về cửa này từ lúc mở → +0.5, dịch ngược → −0.5. Pinnacle không mở mức này thì so mức chính.
    /// </summary>
    public static PatternIndicator SharpPrice(SharpComparison? c, PickSide side, decimal line, decimal odds)
    {
        if (c is null)
        {
            return new PatternIndicator("Giá Pinnacle", 0, "Chưa có kèo Pinnacle (gói Ultra/Mega tự lấy; giải nhỏ Pinnacle có thể không mở)");
        }

        var handicap = side is PickSide.Home or PickSide.Away;
        string Show(decimal l) => handicap ? LineText.Signed(l) : LineText.Number(l);
        double points;
        string text;
        if (c is { Edge: { } e, FairOdds: { } fair, SharpOdds: { } so })
        {
            points = e >= 0.03 ? 1.0 : e >= 0.015 ? 0.5 : e <= -0.03 ? -1.0 : e <= -0.015 ? -0.5 : 0;
            text = $"Pinnacle {Show(line)} @{LineText.Odds(so)} → giá hợp lý {LineText.Odds(fair)}; Bet365 @{LineText.Odds(odds)} "
                + (e >= 0 ? $"cao hơn {Percent(e)}" : $"thấp hơn {Percent(-e)}");
        }
        else if (c.MainLine is { } main)
        {
            // Pinnacle không có đúng mức: mức chính của Pinnacle đòi cửa này nhiều hơn → thị trường chuẩn đánh giá cửa này mạnh hơn Bet365.
            var stronger = side switch { PickSide.Over => main > line, PickSide.Under => main < line, _ => main < line };
            var weaker = main != line && !stronger;
            points = stronger ? 0.5 : weaker ? -0.5 : 0;
            text = $"Pinnacle không mở mức {Show(line)}; mức chính {Show(main)}" + (stronger ? ": đánh giá cửa này mạnh hơn" : weaker ? ": đánh giá cửa này yếu hơn" : "");
        }
        else
        {
            return new PatternIndicator("Giá Pinnacle", 0, "Pinnacle chưa có giá cho mức này");
        }

        if (c.MovedToward(side) is { } toward)
        {
            points += toward ? 0.5 : -0.5;
            text += $" · Pinnacle dịch {Show(c.OpenMainLine!.Value)} → {Show(c.MainLine!.Value)} ({(toward ? "về" : "ngược")} cửa này)";
        }

        return new PatternIndicator("Giá Pinnacle", Math.Clamp(points, -1.5, 1.5), text);
    }

    private static string Percent(double v) => (v * 100).ToString("0.0", Inv) + "%";

    /// <summary>Gợi ý giờ vào kèo.</summary>
    public static EntryAdvice Entry(LinePattern pattern, double score, Lean lean, bool lineupsKnown, DateTime kickoffUtc, DateTime nowUtc, decimal line, decimal? minOdds)
    {
        var price = minOdds is { } m ? $", giá còn ≥ {LineText.Odds(m)}" : "";
        var consider = score < EnterScore ? " Điểm nhóm cân nhắc: tiền nhỏ." : "";
        DateTime Clip(DateTime t) => t < nowUtc ? nowUtc : t;

        if (score < ConsiderScore)
        {
            return new EntryAdvice(EntryTiming.Skip, "Điểm dưới 6: bỏ qua.", null, null);
        }

        if (nowUtc >= kickoffUtc - LastCall)
        {
            return new EntryAdvice(EntryTiming.Skip, "Còn dưới 10 phút tới giờ bóng lăn: bỏ qua, không vào vội.", null, null);
        }

        if (lineupsKnown)
        {
            return new EntryAdvice(EntryTiming.Now, $"Đã có đội hình: vào được ngay nếu mốc vẫn là {LineText.Number(Math.Abs(line))}{price}.{consider}", nowUtc, kickoffUtc - LastCall);
        }

        if (lean == Lean.Toward && score >= EnterScore)
        {
            return new EntryAdvice(EntryTiming.Now, $"Kèo đang chạy về cửa này: vào ngay, càng gần giờ đá giá càng xấu{price}.", nowUtc, kickoffUtc - LastCall);
        }

        if (lean == Lean.Against)
        {
            var worse = pattern == LinePattern.Over3 ? "Tài 3.0 xuống 2.75" : pattern == LinePattern.Favourite1 ? "chấp 1.0 xuống 0.75" : "chấp 0.75 xuống 0.5";
            return new EntryAdvice(EntryTiming.WaitLineups,
                $"Kèo đang dịch ngược: chờ đội hình (~60' trước giờ bóng lăn). Vào khi mốc vẫn là {LineText.Number(Math.Abs(line))}{price}; nếu {worse} thì bỏ.{consider}",
                Clip(kickoffUtc - WindowStart), kickoffUtc - LastCall);
        }

        if (nowUtc >= kickoffUtc - WindowEnd)
        {
            return new EntryAdvice(EntryTiming.Now, $"Đã vào 30' cuối trước giờ bóng lăn: vào ngay nếu mốc vẫn là {LineText.Number(Math.Abs(line))}{price}.{consider}", nowUtc, kickoffUtc - LastCall);
        }

        return new EntryAdvice(EntryTiming.Window, $"Vào trong khung 60–30' trước giờ bóng lăn, sau khi có đội hình{price}.{consider}",
            Clip(kickoffUtc - WindowStart), kickoffUtc - WindowEnd);
    }

    // ------------------------------------------------------------------ Chỉ số

    private static string ProbabilityText(PickProbability p, PickProbability model, double? share)
    {
        var parts = new List<string> { $"Thắng {Pct(p.Win)}" };
        if (p.HalfWin > 0.005)
        {
            parts.Add($"thắng nửa {Pct(p.HalfWin)}");
        }

        if (p.Push > 0.005)
        {
            parts.Add($"hoàn {Pct(p.Push)}");
        }

        parts.Add($"thua {Pct(p.Loss + p.HalfLoss)}");
        var text = string.Join(" · ", parts);
        return share is { } q && model.Share is { } m
            ? $"{text} (mô hình ăn {Pct(m)}, giá nhà cái {Pct(q)})"
            : text;
    }

    private static PatternIndicator Movement(MarketMovement movement, PickSide side, Lean lean, decimal? openLine, decimal? openOdds, decimal line, decimal odds)
    {
        var path = openLine is { } ol && openOdds is { } oo
            ? $"mở {LineText.Number(Math.Abs(ol))} @{LineText.Odds(oo)} → hiện {LineText.Number(Math.Abs(line))} @{LineText.Odds(odds)}"
            : $"hiện {LineText.Number(Math.Abs(line))} @{LineText.Odds(odds)}";
        return lean switch
        {
            Lean.Toward => new PatternIndicator("Biến động kèo", 0.5, $"Kèo dịch về cửa này: {path}"),
            Lean.Against => new PatternIndicator("Biến động kèo", -0.75, $"Kèo dịch ngược cửa này: {path}"),
            _ => new PatternIndicator("Biến động kèo", 0, $"Kèo đứng ({movement.Changes} lần đổi): {path}"),
        };
    }

    private static PatternIndicator WebsitePick(MatchAnalysis analysis, Market market, PickSide side)
    {
        if (analysis.Picks.FirstOrDefault(p => p.Market == market) is not { } pick)
        {
            return new PatternIndicator("Website chấm", 0, "Website chưa chấm loại kèo này");
        }

        var g = AnalysisGroupText.Short(pick.Group);
        var text = $"Website chấm {pick.Label} nhóm {g} ({pick.Score.ToString("0.0", Inv)})";
        if (pick.Side == side)
        {
            return new PatternIndicator("Website chấm", pick.Group switch { AnalysisGroup.A => 0.75, AnalysisGroup.B => 0.25, _ => 0 }, "Cùng cửa: " + text);
        }

        return new PatternIndicator("Website chấm", pick.Group switch { AnalysisGroup.A => -1.0, AnalysisGroup.B => -0.5, _ => 0 }, "Ngược cửa: " + text);
    }

    private static PatternIndicator ExpectedGoals(MatchAnalysis a)
    {
        var total = a.LambdaHome + a.LambdaAway;
        var text = $"Kỳ vọng {Num(total)} bàn (nhà {Num(a.LambdaHome)} + khách {Num(a.LambdaAway)})";
        return total >= 3.3 ? new PatternIndicator("Tổng bàn kỳ vọng", 0.5, text + ": đủ cho Tài 3")
            : total <= 2.6 ? new PatternIndicator("Tổng bàn kỳ vọng", -0.75, text + ": thấp so với mốc 3")
            : new PatternIndicator("Tổng bàn kỳ vọng", 0, text);
    }

    private static PatternIndicator OverForm(MatchAnalysis a)
    {
        var avg = (a.HomeForm.Over25Rate + a.AwayForm.Over25Rate) / 2;
        var text = $"Trận Tài 2.5 gần đây: nhà {Pct(a.HomeForm.Over25Rate)}, khách {Pct(a.AwayForm.Over25Rate)} (TB bàn {Num(a.HomeForm.TotalGoals)} / {Num(a.AwayForm.TotalGoals)})";
        return avg >= 0.65 ? new PatternIndicator("Phong độ Tài", 0.5, text)
            : avg <= 0.40 ? new PatternIndicator("Phong độ Tài", -0.5, text)
            : new PatternIndicator("Phong độ Tài", 0, text);
    }

    private static PatternIndicator OverDna(AnalysisInput input)
    {
        var league = input.LeagueDna?.Tags ?? DnaTag.None;
        var home = input.HomeDna?.Tags ?? DnaTag.None;
        var away = input.AwayDna?.Tags ?? DnaTag.None;
        var plus = new List<string>();
        var minus = new List<string>();
        if (league.HasFlag(DnaTag.OverGoals)) plus.Add("giải Tài");
        if (home.HasFlag(DnaTag.OverGoals)) plus.Add($"{input.HomeName} nổ Tài");
        if (away.HasFlag(DnaTag.OverGoals)) plus.Add($"{input.AwayName} nổ Tài");
        if (home.HasFlag(DnaTag.Btts) || away.HasFlag(DnaTag.Btts)) plus.Add("công làm thủ phá");
        if (league.HasFlag(DnaTag.UnderGoals)) minus.Add("giải Xỉu");
        if (league.HasFlag(DnaTag.Pragmatic)) minus.Add("giải vua hòa");
        if (home.HasFlag(DnaTag.UnderGoals) || home.HasFlag(DnaTag.Pragmatic)) minus.Add($"{input.HomeName} thực dụng");
        if (away.HasFlag(DnaTag.UnderGoals) || away.HasFlag(DnaTag.Pragmatic)) minus.Add($"{input.AwayName} thực dụng");
        var points = (plus.Count > 0 ? 0.5 : 0) - (minus.Count > 0 ? 0.5 : 0);
        var text = plus.Count + minus.Count == 0 ? "Không có nhãn DNA liên quan" : string.Join(" · ", plus.Select(x => "+ " + x).Concat(minus.Select(x => "− " + x)));
        return new PatternIndicator("DNA", points, text);
    }

    private static PatternIndicator Projection(MatchAnalysis a, bool favHome)
    {
        if (a.Handicap.Delta is not { } delta)
        {
            return new PatternIndicator("Dự phóng chấp", 0, "Chưa có kèo để so");
        }

        // Delta = kèo − dự phóng (nhìn từ đội nhà): dương là đội nhà mạnh hơn kèo.
        var value = favHome ? delta : -delta;
        var text = $"Dự phóng {LineText.Signed(a.Handicap.HExp)} vs kèo {LineText.Signed(a.Handicap.HMarket ?? 0)} (nhìn từ đội nhà)";
        return value >= 0.25m ? new PatternIndicator("Dự phóng chấp", 0.5, text + ": cửa trên mạnh hơn kèo")
            : value <= -0.25m ? new PatternIndicator("Dự phóng chấp", -0.75, text + ": cửa trên yếu hơn kèo")
            : new PatternIndicator("Dự phóng chấp", 0, text + ": sát kèo");
    }

    private static PatternIndicator FavouriteDna(AnalysisInput input, bool favHome)
    {
        var league = input.LeagueDna?.Tags ?? DnaTag.None;
        var fav = (favHome ? input.HomeDna?.Tags : input.AwayDna?.Tags) ?? DnaTag.None;
        var dog = (favHome ? input.AwayDna?.Tags : input.HomeDna?.Tags) ?? DnaTag.None;
        var favName = favHome ? input.HomeName : input.AwayName;
        var dogName = favHome ? input.AwayName : input.HomeName;
        var plus = new List<string>();
        var minus = new List<string>();
        if (favHome && fav.HasFlag(DnaTag.HomeFortress)) plus.Add($"{favName} mạnh sân nhà");
        if (fav.HasFlag(DnaTag.BullyWeak)) plus.Add($"{favName} bắt nạt đội yếu");
        if (league.HasFlag(DnaTag.BullyWeak)) plus.Add("giải tín kèo trên");
        if (favHome && dog.HasFlag(DnaTag.AwayFragile)) plus.Add($"{dogName} yếu sân khách");
        if (league.HasFlag(DnaTag.UnderdogCover)) minus.Add("giải cửa dưới hay ăn");
        if (league.HasFlag(DnaTag.Pragmatic)) minus.Add("giải vua hòa / thắng sát nút");
        if (!favHome && fav.HasFlag(DnaTag.AwayFragile)) minus.Add($"{favName} yếu sân khách");
        if (!favHome && dog.HasFlag(DnaTag.HomeFortress)) minus.Add($"{dogName} mạnh sân nhà");
        if (fav.HasFlag(DnaTag.Pragmatic)) minus.Add($"{favName} thực dụng, hay thắng sát nút");
        var points = (plus.Count > 0 ? 0.5 : 0) - (minus.Count > 0 ? 0.5 : 0);
        var text = plus.Count + minus.Count == 0 ? "Không có nhãn DNA liên quan" : string.Join(" · ", plus.Select(x => "+ " + x).Concat(minus.Select(x => "− " + x)));
        return new PatternIndicator("DNA", points, text);
    }

    private static PatternIndicator Staffing(AnalysisInput input, MatchAnalysis a, bool favHome)
    {
        var regime = favHome ? a.HomeRegime : a.AwayRegime;
        var absent = favHome ? a.HomeAbsent : a.AwayAbsent;
        var name = favHome ? input.HomeName : input.AwayName;
        var points = 0.0;
        var notes = new List<string>();
        if (regime is { InBuffer: true })
        {
            points -= 0.5;
            notes.Add($"{regime.KindText} {regime.PersonName}: trận thứ {regime.UpcomingMatchNumber}");
        }

        if (absent.Count >= 3)
        {
            points -= 0.5;
            notes.Add($"vắng {absent.Count} người");
        }

        return new PatternIndicator("Nhân sự cửa trên", points, notes.Count == 0 ? $"{name}: không có biến động lớn" : $"{name}: {string.Join(", ", notes)}");
    }

    private static string Pct(double v) => (v * 100).ToString("0", Inv) + "%";

    private static string Num(double v) => v.ToString("0.00", Inv);

    private static string Signed(double v) => (v >= 0 ? "+" : "−") + Math.Abs(v).ToString("0.00", Inv);
}
