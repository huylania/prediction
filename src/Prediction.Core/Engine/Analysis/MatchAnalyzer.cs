using System.Globalization;
using Prediction.Core.Entities;

namespace Prediction.Core.Engine.Analysis;

/// <summary>
/// Phân tích trước trận theo file kỹ năng "bong-da": DNA giải/đội, quy tắc chờ ổn định 2–3 trận,
/// kèo chấp dự phóng H_exp và kèo lạ, 5 cờ đỏ, đối chiếu 3 lớp bàn thắng/phạt góc/thẻ, xác suất Poisson và thang điểm 10.
/// </summary>
public static class MatchAnalyzer
{
    // Ngưỡng theo file kỹ năng.
    public const double GoalsDeltaThreshold = 0.40;
    public const double GoalsTrapDelta = 0.75;
    public const double CornersDeltaThreshold = 1.2;
    public const double CornerHandicapGap = 2.0;
    public const double CardsDeltaThreshold = 0.8;
    public const decimal AnomalyDelta = 0.5m;
    public const decimal AnomalyMove = 0.5m;

    // Lợi thế sân nhà (bàn) khi tính H_exp.
    public const double BaseHomeAdvantage = 0.30;
    public const double FortressLeagueHomeAdvantage = 0.60;
    public const double TeamVenueAdjustment = 0.25;

    /// <summary>Sân từ độ cao này (m) được cộng thêm lợi thế sân nhà (Bolivia, Ecuador, Colombia...).</summary>
    public const int HighAltitudeM = 2000;

    public const double FormWeight = 0.6;
    public const double TableWeight = 0.4;

    /// <summary>Mẫu ít hơn số trận này thì kéo trung bình về trung bình giải.</summary>
    public const int MinSample = 5;

    /// <summary>Tỉ trọng xG trong số bàn kỳ vọng khi đội có ≥ <see cref="MinSample"/> trận có xG (phần còn lại là bàn thắng thật).</summary>
    public const double XgWeight = 0.5;

    /// <summary>
    /// Trộn xác suất mô hình với giá nhà cái (đã bỏ lời): tin mô hình 30% + 3%/trận mẫu, tối đa 60% (đủ 10 trận).
    /// Mẫu ít thì kéo mạnh về giá thị trường — tránh "thắng 75%" từ 5 trận đầu mùa.
    /// </summary>
    public const double ModelWeightMin = 0.30;
    public const double ModelWeightPerMatch = 0.03;
    public const double ModelWeightMax = 0.60;

    /// <summary>Mô hình lệch giá nhà cái từ chừng này trở lên mà mẫu dưới <see cref="ReliableSample"/> trận: tối đa nhóm B.</summary>
    public const double MarketDivergenceCap = 0.20;
    public const int ReliableSample = 10;

    /// <summary>Tổng lợi thế sân nhà theo DNA (giải + đội) tối đa, theo khung +0.50 → +0.75 của file kỹ năng.</summary>
    public const double MaxDnaHomeAdvantage = 0.75;

    public const double CornerDispersion = 1.4;
    public const double CardDispersion = 1.6;

    private static readonly CultureInfo Vi = CultureInfo.InvariantCulture;

    public static MatchAnalysis Analyze(AnalysisInput input)
    {
        var f = input.Fixture;
        var kickoff = f.KickoffUtc;
        var league = input.League;
        var leagueTags = input.LeagueDna?.Tags ?? DnaTag.None;
        var homeTags = input.HomeDna?.Tags ?? DnaTag.None;
        var awayTags = input.AwayDna?.Tags ?? DnaTag.None;

        // 1. Nhân sự
        var homeRegime = TeamFormBuilder.Regime(input.HomeChanges, input.HomeHistory, kickoff);
        var awayRegime = TeamFormBuilder.Regime(input.AwayChanges, input.AwayHistory, kickoff);
        var homeForm = TeamFormBuilder.Build(input.HomeHistory, true, homeRegime);
        var awayForm = TeamFormBuilder.Build(input.AwayHistory, false, awayRegime);
        var homeAbsent = input.Injuries.Where(i => i.TeamId == f.HomeTeamId).ToList();
        var awayAbsent = input.Injuries.Where(i => i.TeamId == f.AwayTeamId).ToList();
        var homeRest = RestDays(input.HomeHistory, kickoff);
        var awayRest = RestDays(input.AwayHistory, kickoff);

        var ctx = new Ctx(input, leagueTags, homeTags, awayTags, homeRegime, awayRegime, homeForm, awayForm,
            homeAbsent.Count(i => i.IsMissing), awayAbsent.Count(i => i.IsMissing), homeRest, awayRest);

        // 4a. Bàn thắng: kỳ vọng kết hợp (bàn thật trộn xG khi đủ mẫu; mẫu ít trận kéo về trung bình giải).
        var gHalf = league.Goals / 2;
        var gsH = Shrink(ScoringRate(homeForm), homeForm.Matches, gHalf);
        var gcH = Shrink(ConcedingRate(homeForm), homeForm.Matches, gHalf);
        var gsA = Shrink(ScoringRate(awayForm), awayForm.Matches, gHalf);
        var gcA = Shrink(ConcedingRate(awayForm), awayForm.Matches, gHalf);
        var lambdaHome0 = (gsH + gcA) / 2;
        var lambdaAway0 = (gsA + gcH) / 2;
        var expGoals = lambdaHome0 + lambdaAway0;

        // 2. Kèo chấp dự phóng
        var handicap = BuildHandicap(ctx, lambdaHome0 - lambdaAway0);
        var supremacy = handicap.Supremacy;
        var lambdaHome = Math.Max(0.15, (expGoals + supremacy) / 2);
        var lambdaAway = Math.Max(0.15, (expGoals - supremacy) / 2);
        var homeGoals = Distributions.Poisson(lambdaHome, Distributions.MaxGoals);
        var awayGoals = Distributions.Poisson(lambdaAway, Distributions.MaxGoals);
        var goalDiff = Distributions.Difference(homeGoals, awayGoals);
        var goalTotal = Distributions.Sum(homeGoals, awayGoals);

        // 4b. Phạt góc
        var cHalf = league.Corners / 2;
        // Mỗi đội cần đủ mẫu thống kê góc: mẫu quá ít bị kéo về TB giải làm 2 đội "ngang nhau" → cửa dưới chấp góc sâu ảo.
        var cornersKnown = homeForm.CornerMatches >= MinSample && awayForm.CornerMatches >= MinSample;
        var cwH = Shrink(Blend(homeForm.CornersFor, homeForm.VenueCornersFor, homeForm.VenueCornerMatches) ?? cHalf, homeForm.CornerMatches, cHalf);
        var ccH = Shrink(Blend(homeForm.CornersAgainst, homeForm.VenueCornersAgainst, homeForm.VenueCornerMatches) ?? cHalf, homeForm.CornerMatches, cHalf);
        var cwA = Shrink(Blend(awayForm.CornersFor, awayForm.VenueCornersFor, awayForm.VenueCornerMatches) ?? cHalf, awayForm.CornerMatches, cHalf);
        var ccA = Shrink(Blend(awayForm.CornersAgainst, awayForm.VenueCornersAgainst, awayForm.VenueCornerMatches) ?? cHalf, awayForm.CornerMatches, cHalf);
        var cornersHome = (cwH + ccA) / 2;
        var cornersAway = (cwA + ccH) / 2;
        var expCorners = cornersHome + cornersAway;
        var homeCornerDist = Distributions.NegativeBinomial(cornersHome, CornerDispersion, Distributions.MaxCorners / 2);
        var awayCornerDist = Distributions.NegativeBinomial(cornersAway, CornerDispersion, Distributions.MaxCorners / 2);

        // 4c. Thẻ
        var kHalf = league.Cards / 2;
        var cardsKnown = homeForm.CardsFor.HasValue && awayForm.CardsFor.HasValue;
        var expCards = cardsKnown
            ? (Shrink(homeForm.CardsFor!.Value, homeForm.Matches, kHalf) + Shrink(awayForm.CardsAgainst ?? kHalf, awayForm.Matches, kHalf)
               + Shrink(awayForm.CardsFor!.Value, awayForm.Matches, kHalf) + Shrink(homeForm.CardsAgainst ?? kHalf, homeForm.Matches, kHalf)) / 2
            : league.Cards;

        var goalsRow = GoalsRow(ctx, expGoals);
        var cornersRow = CornersRow(ctx, cornersKnown ? expCorners : null);
        var cornerHandicapRow = CornerHandicapRow(ctx, cornersKnown ? (cwH, cwA) : null);
        var cardsRow = CardsRow(ctx, cardsKnown ? expCards : null);

        // 3. Cờ đỏ
        var flags = RedFlags(ctx, handicap, expGoals);

        // 6. Khuyến nghị
        var picks = new List<AnalysisPickResult>();
        AddHandicapPick(picks, ctx, handicap, goalDiff);
        AddTotalsPick(picks, ctx, Market.OverUnder, input.Totals, goalTotal, goalsRow, "bàn");
        if (cornersKnown)
        {
            AddTotalsPick(picks, ctx, Market.CornersOverUnder, input.Corners, Distributions.Sum(homeCornerDist, awayCornerDist), cornersRow, "góc");
            AddCornerHandicapPick(picks, ctx, Distributions.Difference(homeCornerDist, awayCornerDist), cornerHandicapRow);
        }

        if (cardsKnown)
        {
            AddTotalsPick(picks, ctx, Market.CardsOverUnder, input.Cards, Distributions.NegativeBinomial(expCards, CardDispersion, Distributions.MaxCards), cardsRow, "thẻ");
        }

        foreach (var pick in picks)
        {
            Grade(pick, ctx, flags);
        }

        var ordered = picks
            .OrderBy(p => p.Group)
            .ThenByDescending(p => p.Score)
            .ToList();

        var homeWin = goalDiff.Diff.Skip(goalDiff.Offset + 1).Sum();
        var draw = goalDiff.Diff[goalDiff.Offset];

        return new MatchAnalysis
        {
            FixtureId = f.Id,
            LeagueTags = DnaTagText.Labels(leagueTags, false).ToList(),
            HomeTags = DnaTagText.Labels(homeTags, true).ToList(),
            AwayTags = DnaTagText.Labels(awayTags, true).ToList(),
            LeagueNotes = input.LeagueDna?.Notes,
            HomeNotes = input.HomeDna?.Notes,
            AwayNotes = input.AwayDna?.Notes,
            HomeRegime = homeRegime,
            AwayRegime = awayRegime,
            HomeAbsent = homeAbsent,
            AwayAbsent = awayAbsent,
            HomeRestDays = homeRest,
            AwayRestDays = awayRest,
            Handicap = handicap,
            Flags = flags,
            HomeForm = homeForm,
            AwayForm = awayForm,
            Goals = goalsRow,
            CornersRow = cornersRow,
            CornerHandicap = cornerHandicapRow,
            CardsRow = cardsRow,
            LambdaHome = lambdaHome,
            LambdaAway = lambdaAway,
            CornersHome = cornersHome,
            CornersAway = cornersAway,
            ExpectedCards = expCards,
            HomeWinProbability = homeWin,
            DrawProbability = draw,
            AwayWinProbability = Math.Max(0, 1 - homeWin - draw),
            InPlayHints = InPlayHints(ctx, handicap, cornersRow),
            Picks = ordered,
            LowData = homeForm.Matches < MinSample || awayForm.Matches < MinSample,
        };
    }

    // ---------------------------------------------------------------- Kèo chấp dự phóng

    private static HandicapModel BuildHandicap(Ctx c, double formPart)
    {
        var input = c.Input;
        double? tablePart = input.HomeStanding is { Played: >= 3 } hs && input.AwayStanding is { Played: >= 3 } aws
            ? ((double)hs.GoalDiff / hs.Played - (double)aws.GoalDiff / aws.Played) / 2
            : null;

        var notes = new List<string>();
        var ha = BaseHomeAdvantage;
        if (c.LeagueTags.HasFlag(DnaTag.HomeFortress))
        {
            ha = FortressLeagueHomeAdvantage;
            notes.Add($"giải thánh địa sân nhà +{Num(FortressLeagueHomeAdvantage)}");
        }
        else
        {
            notes.Add($"sân nhà +{Num(BaseHomeAdvantage)}");
        }

        if (c.HomeTags.HasFlag(DnaTag.HomeFortress))
        {
            ha += TeamVenueAdjustment;
            notes.Add($"{input.HomeName} mạnh sân nhà +{Num(TeamVenueAdjustment)}");
        }

        if (c.AwayTags.HasFlag(DnaTag.AwayFragile))
        {
            ha += TeamVenueAdjustment;
            notes.Add($"{input.AwayName} yếu sân khách +{Num(TeamVenueAdjustment)}");
        }

        if (ha > MaxDnaHomeAdvantage)
        {
            // Giải thánh địa + đội mạnh nhà + đối thủ yếu khách là các mặt của cùng một lợi thế: không cộng dồn quá khung.
            ha = MaxDnaHomeAdvantage;
            notes.Add($"tổng lợi thế sân theo DNA tối đa +{Num(MaxDnaHomeAdvantage)}");
        }

        if (input.VenueElevationM >= HighAltitudeM)
        {
            ha += TeamVenueAdjustment;
            notes.Add($"sân cao {input.VenueElevationM} m +{Num(TeamVenueAdjustment)}");
        }

        var supremacy = tablePart is { } t ? FormWeight * formPart + TableWeight * t + ha : formPart + ha;
        var hExp = -RoundQuarter(supremacy);
        var hMarket = input.Handicap?.Current.Line;
        var hOpen = input.Handicap?.Open.Line;

        var delta = hMarket - hExp;
        var move = hMarket - hOpen;
        var deltaAnomaly = delta is { } d && Math.Abs(d) >= AnomalyDelta;
        var moveAnomaly = move is { } m && Math.Abs(m) >= AnomalyMove;

        // Lệch dự phóng mà nhà cái không dịch kèo: chỉ coi là "kèo lạ" khi dự phóng đáng tin.
        string? unreliable = null;
        if (deltaAnomaly && !moveAnomaly)
        {
            var minMatches = Math.Min(c.HomeForm.Matches, c.AwayForm.Matches);
            if (minMatches < MinSample)
            {
                unreliable = $"Dự phóng chỉ dựa trên {minMatches} trận: lệch {Signed(delta!.Value)} với nhà cái nhiều khả năng do mô hình thiếu dữ liệu, không phải kèo lạ";
            }
            else if (string.Equals(input.LeagueType, "Cup", StringComparison.OrdinalIgnoreCase))
            {
                unreliable = $"Trận cúp: phong độ hai đội đến từ các giải khác đẳng cấp nên dự phóng kém tin cậy; nhà cái không dịch kèo (lệch {Signed(delta!.Value)}) → không coi là kèo lạ";
            }
        }

        c.HandicapUnreliable = unreliable;
        var anomaly = deltaAnomaly && unreliable is null || moveAnomaly;

        var scenario = HandicapScenario.None;
        if (hMarket is { } line && input.HomeStanding?.Rank is { } rh && input.AwayStanding?.Rank is { } ra && Math.Min(input.HomeStanding.Played, input.AwayStanding!.Played) >= 3)
        {
            var size = input.TableSize ?? 20;
            if (Math.Abs(rh - ra) <= 3 && Math.Abs(line) >= 0.75m)
            {
                scenario = HandicapScenario.DeepLineCloseRanks;
            }
            else if (rh < ra && line >= -0.25m && (ra - rh >= 5 || rh <= size / 2 && ra > size / 2))
            {
                scenario = HandicapScenario.ShallowLineForBetterHome;
            }
        }

        var causes = new List<string>();
        var verdict = HandicapVerdict.Normal;
        string summary;
        if (hMarket is null)
        {
            summary = $"Kèo chấp dự phóng: {LineText.HandicapSentence(hExp, input.HomeName, input.AwayName)} (chưa có kèo nhà cái để so).";
        }
        else if (anomaly || scenario != HandicapScenario.None)
        {
            // Phía thị trường đánh giá cao hơn mô hình: Δ < 0 → thị trường nghiêng về đội nhà.
            var marketTowardHome = scenario switch
            {
                HandicapScenario.DeepLineCloseRanks => hMarket < 0,
                HandicapScenario.ShallowLineForBetterHome => false,
                _ => delta is { } dd && dd != 0 ? dd < 0 : move < 0,
            };
            causes.AddRange(Causes(c, marketTowardHome));
            if (scenario == HandicapScenario.DeepLineCloseRanks && delta is { } agree && Math.Abs(agree) <= 0.25m)
            {
                causes.Add($"phong độ gần đây ủng hộ mức chấp sâu (dự phóng {Sentence(hExp, input)})");
            }

            verdict = causes.Count > 0 ? HandicapVerdict.RealStrength : HandicapVerdict.SuspectedTrap;
            var what = scenario switch
            {
                HandicapScenario.DeepLineCloseRanks => $"Kịch bản A: hai đội xếp sát nhau (hạng {input.HomeStanding!.Rank} và {input.AwayStanding!.Rank}) nhưng kèo chấp sâu {LineText.Handicap(hMarket.Value)}",
                HandicapScenario.ShallowLineForBetterHome => $"Kịch bản B: {input.HomeName} xếp trên (hạng {input.HomeStanding!.Rank} vs {input.AwayStanding!.Rank}) đá sân nhà nhưng kèo chỉ {Sentence(hMarket.Value, input)}",
                _ => $"Kèo lạ: nhà cái {Sentence(hMarket.Value, input)} so với dự phóng {Sentence(hExp, input)}",
            };
            var favouriteName = hMarket < 0 ? input.HomeName : input.AwayName;
            summary = verdict == HandicapVerdict.RealStrength
                ? $"{what}. Tìm thấy nguyên nhân → KÈO THỰC LỰC (thị trường đã phản ánh lực lượng)."
                : scenario == HandicapScenario.ShallowLineForBetterHome
                    ? $"{what}. Không thấy nguyên nhân → KÈO THƠM ẢO: không chọn {input.HomeName} trước trận; cân nhắc cửa dưới / Xỉu hoặc bỏ qua."
                    : anomaly
                        ? $"{what}. Không thấy nguyên nhân chấn thương/lịch thi đấu → NGHI BẪY, bỏ kèo chấp và bàn thắng trước trận."
                        : $"{what}. Không thấy nguyên nhân → BẪY DỤ CỬA TRÊN: không chọn {favouriteName}, cân nhắc cửa dưới.";
        }
        else
        {
            summary = unreliable is not null
                ? $"{unreliable}. Kèo chấp chỉ tham khảo (tối đa nhóm B)."
                : delta is { } dd && Math.Abs(dd) <= 0.25m
                    ? "Kèo nhà cái sát với dự phóng (|Δ_H| ≤ 0.25): đánh giá theo chuyên môn, DNA và chiến thuật."
                    : $"Kèo nhà cái lệch {Signed(delta!.Value)} so với dự phóng: có giá trị nhẹ cho {(delta > 0 ? input.HomeName : input.AwayName)}.";
        }

        return new HandicapModel
        {
            FormPart = formPart,
            TablePart = tablePart,
            HomeAdvantage = ha,
            HomeAdvantageNotes = notes,
            Supremacy = supremacy,
            HExp = hExp,
            HMarket = hMarket,
            HOpen = hOpen,
            IsAnomaly = anomaly,
            Scenario = scenario,
            Causes = causes,
            Verdict = verdict,
            Summary = summary,
        };
    }

    /// <summary>Nguyên cớ giải thích việc thị trường nghiêng về một đội nhiều hơn mô hình.</summary>
    private static List<string> Causes(Ctx c, bool marketTowardHome)
    {
        var input = c.Input;
        var (strongName, weakName) = marketTowardHome ? (input.HomeName, input.AwayName) : (input.AwayName, input.HomeName);
        var (weakMissing, strongMissing) = marketTowardHome ? (c.AwayMissing, c.HomeMissing) : (c.HomeMissing, c.AwayMissing);
        var (weakRest, strongRest) = marketTowardHome ? (c.AwayRest, c.HomeRest) : (c.HomeRest, c.AwayRest);
        var (weakRegime, strongRegime) = marketTowardHome ? (c.AwayRegime, c.HomeRegime) : (c.HomeRegime, c.AwayRegime);

        var causes = new List<string>();
        if (weakMissing >= 3 && weakMissing >= strongMissing + 2)
        {
            causes.Add($"{weakName} vắng {weakMissing} cầu thủ (danh sách chấn thương/treo giò)");
        }

        if (weakRest is < 4 && (strongRest is null or >= 4))
        {
            causes.Add($"{weakName} vừa đá cách đây {weakRest:0.#} ngày (lịch dày/đá giữa tuần → nguy cơ xoay tua, kiệt sức)");
        }

        if (weakRegime is { InBuffer: true })
        {
            causes.Add($"{weakName} đang lắp ghép {weakRegime.KindText} ({weakRegime.PersonName}, trận thứ {weakRegime.UpcomingMatchNumber})");
        }

        if (strongRegime is { InBuffer: false, UsesNewSample: true })
        {
            causes.Add($"{strongName} đã qua giai đoạn ổn định với {strongRegime.KindText} {strongRegime.PersonName} ({strongRegime.MatchesUnder} trận)");
        }

        if (marketTowardHome)
        {
            if (c.LeagueTags.HasFlag(DnaTag.HomeFortress) || c.HomeTags.HasFlag(DnaTag.HomeFortress))
            {
                causes.Add($"DNA sân: {input.HomeName} có lợi thế sân nhà cực mạnh");
            }

            if (c.AwayTags.HasFlag(DnaTag.AwayFragile))
            {
                causes.Add($"DNA sân: {input.AwayName} yếu khi đá sân khách");
            }
        }

        return causes;
    }

    // ---------------------------------------------------------------- Bảng 3 lớp

    private static LayerRow GoalsRow(Ctx c, double expGoals)
    {
        var input = c.Input;
        var leagueAvg = input.League.Goals;
        var line = input.Totals?.Current.Line;
        double? delta = line is { } l ? expGoals - (double)l : null;
        PickSide? lean = null;
        string verdict;
        if (delta is { } d && d >= GoalsDeltaThreshold && expGoals >= leagueAvg)
        {
            lean = PickSide.Over;
            verdict = $"Tài: Δ = {Signed(d)} ≥ +0.40 và kỳ vọng {Num(expGoals)} ≥ TB giải {Num(leagueAvg)}";
        }
        else if (delta is { } d2 && d2 <= -GoalsDeltaThreshold && expGoals <= leagueAvg)
        {
            lean = PickSide.Under;
            verdict = $"Xỉu: Δ = {Signed(d2)} ≤ −0.40 và kỳ vọng {Num(expGoals)} ≤ TB giải {Num(leagueAvg)}";
        }
        else
        {
            verdict = line is null ? "Chưa có kèo tài xỉu." : "Chưa đủ biên an toàn (cần |Δ| ≥ 0.40 cùng chiều TB giải).";
        }

        verdict += XgNote(c);

        return new LayerRow
        {
            Name = "Bàn thắng",
            LeagueAverage = leagueAvg,
            LeagueDnaText = Hint(DnaText(c.LeagueTags, DnaTag.OverGoals, DnaTag.UnderGoals),
                c.LeagueTags.HasFlag(DnaTag.Pragmatic) ? DnaTagText.Label(DnaTag.Pragmatic, false) : null,
                input.League.GoalsFromProfile ? ProfileText : null),
            HomeText = $"Ghi {Num(c.HomeForm.GoalsFor)} / Thủng {Num(c.HomeForm.GoalsAgainst)}{XgText(c.HomeForm)} (TB {Num(c.HomeForm.TotalGoals)}, Tài 2.5: {Pct(c.HomeForm.Over25Rate)})",
            AwayText = $"Ghi {Num(c.AwayForm.GoalsFor)} / Thủng {Num(c.AwayForm.GoalsAgainst)}{XgText(c.AwayForm)} (TB {Num(c.AwayForm.TotalGoals)}, Tài 2.5: {Pct(c.AwayForm.Over25Rate)})",
            Expected = expGoals,
            MarketLine = line,
            MarketText = MoveText(input.Totals),
            Delta = delta,
            Lean = lean,
            Verdict = verdict,
        };
    }

    private static LayerRow CornersRow(Ctx c, double? expCorners)
    {
        var input = c.Input;
        var leagueAvg = input.League.Corners;
        var line = input.Corners?.Current.Line;
        double? delta = line is { } l && expCorners is { } e ? e - (double)l : null;
        PickSide? lean = null;
        string verdict;
        if (expCorners is null)
        {
            verdict = CornerSampleText(c) ?? "Chưa có thống kê phạt góc của hai đội.";
        }
        else if (delta is { } d && d >= CornersDeltaThreshold && expCorners >= leagueAvg && CornerStyleOver(c, out var why))
        {
            lean = PickSide.Over;
            verdict = $"Tài góc: Δ = {Signed(d)} ≥ +1.2, kỳ vọng ≥ TB giải {Num(leagueAvg)}, {why}";
        }
        else if (delta is { } d2 && d2 <= -CornersDeltaThreshold && expCorners <= leagueAvg && CornerStyleUnder(c, out var why2))
        {
            lean = PickSide.Under;
            verdict = $"Xỉu góc: Δ = {Signed(d2)} ≤ −1.2, kỳ vọng ≤ TB giải {Num(leagueAvg)}, {why2}";
        }
        else
        {
            verdict = line is null ? "Chưa có kèo tài xỉu góc." : "Chưa đủ điều kiện (|Δ| ≥ 1.2 + TB giải + phong cách).";
        }

        return new LayerRow
        {
            Name = "Phạt góc",
            LeagueAverage = input.League.CornersKnown ? leagueAvg : null,
            LeagueDnaText = Hint(DnaText(c.LeagueTags, DnaTag.OverCorners, DnaTag.UnderCorners), c.Input.League.CornersFromProfile ? ProfileText : null,
                c.Input.League.CornersKnown ? null : NoLeagueStatsText),
            HomeText = c.HomeForm.CornersFor is { } hw
                ? $"Kiếm {Num(hw)} / Bị {Num(c.HomeForm.CornersAgainst ?? 0)} (sân nhà: {Num(c.HomeForm.VenueCornersFor)}/{Num(c.HomeForm.VenueCornersAgainst)}, Tài 9.5: {Pct(c.HomeForm.Over95CornersRate ?? 0)})"
                : "—",
            AwayText = c.AwayForm.CornersFor is { } aw
                ? $"Kiếm {Num(aw)} / Bị {Num(c.AwayForm.CornersAgainst ?? 0)} (sân khách: {Num(c.AwayForm.VenueCornersFor)}/{Num(c.AwayForm.VenueCornersAgainst)}, Tài 9.5: {Pct(c.AwayForm.Over95CornersRate ?? 0)})"
                : "—",
            Expected = expCorners,
            MarketLine = line,
            MarketText = MoveText(input.Corners, "không xét dòng tiền"),
            Delta = delta,
            Lean = lean,
            Verdict = verdict,
        };
    }

    private static LayerRow CornerHandicapRow(Ctx c, (double Home, double Away)? won)
    {
        var input = c.Input;
        var line = input.CornersHandicap?.Current.Line;
        double? gap = won is { } w ? w.Home - w.Away : null;
        PickSide? lean = null;
        string verdict;
        if (gap is null)
        {
            verdict = CornerSampleText(c) ?? "Chưa có thống kê phạt góc.";
        }
        else if (line is null)
        {
            verdict = "Chưa có kèo chấp góc.";
        }
        else if (gap >= CornerHandicapGap && -(double)line.Value < gap)
        {
            lean = PickSide.Home;
            verdict = $"{input.HomeName} kiếm hơn {Num(gap.Value)} góc/trận, nhà cái chỉ chấp {Num(-(double)line.Value)} → chấp góc {input.HomeName}";
        }
        else if (gap <= -CornerHandicapGap && (double)line.Value < -gap)
        {
            lean = PickSide.Away;
            verdict = $"{input.AwayName} kiếm hơn {Num(-gap.Value)} góc/trận, nhà cái chỉ chấp {Num((double)line.Value)} → chấp góc {input.AwayName}";
        }
        else
        {
            verdict = "Chênh lệch góc chưa đạt 2.0/trận hoặc mức chấp đã phản ánh đủ.";
        }

        return new LayerRow
        {
            Name = "Chấp góc",
            HomeText = won is { } h ? $"Kiếm {Num(h.Home)} góc/trận" : "—",
            AwayText = won is { } a ? $"Kiếm {Num(a.Away)} góc/trận" : "—",
            Expected = gap,
            MarketLine = line,
            MarketText = line is { } l ? $"{input.HomeName} {Signed(l)}" : "—",
            Delta = gap is { } g && line is { } l2 ? g + (double)l2 : null,
            Lean = lean,
            Verdict = verdict,
        };
    }

    /// <summary>Có thống kê góc nhưng chưa đủ mẫu: ghi số trận để biết vì sao không khuyến nghị kèo góc.</summary>
    private static string? CornerSampleText(Ctx c) =>
        c.HomeForm.CornerMatches + c.AwayForm.CornerMatches == 0
            ? null
            : $"Chưa đủ thống kê phạt góc ({c.Input.HomeName} {c.HomeForm.CornerMatches} / {c.Input.AwayName} {c.AwayForm.CornerMatches} trận, cần ≥ {MinSample} mỗi đội) → không khuyến nghị kèo góc.";

    private static LayerRow CardsRow(Ctx c, double? expCards)
    {
        var input = c.Input;
        var leagueAvg = input.League.Cards;
        var line = input.Cards?.Current.Line;
        double? delta = line is { } l && expCards is { } e ? e - (double)l : null;
        PickSide? lean = null;
        string verdict;
        if (expCards is null)
        {
            verdict = "Chưa có thống kê thẻ phạt.";
        }
        else if (delta is { } d && d >= CardsDeltaThreshold && expCards >= leagueAvg)
        {
            lean = PickSide.Over;
            verdict = $"Tài thẻ (tham khảo): kỳ vọng {Num(expCards.Value)} > mốc {Signed(d)}";
        }
        else if (delta is { } d2 && d2 <= -CardsDeltaThreshold && expCards <= leagueAvg)
        {
            lean = PickSide.Under;
            verdict = $"Xỉu thẻ (tham khảo): kỳ vọng {Num(expCards.Value)} < mốc {Signed(d2)}";
        }
        else
        {
            verdict = line is null ? "Chưa có kèo thẻ." : "Không có độ lệch rõ (tham khảo: còn phụ thuộc trọng tài, tính chất trận).";
        }

        return new LayerRow
        {
            Name = "Thẻ phạt",
            LeagueAverage = input.League.CardsKnown ? leagueAvg : null,
            LeagueDnaText = Hint(
                c.LeagueTags.HasFlag(DnaTag.HeavyCards) ? DnaTagText.Label(DnaTag.HeavyCards, false)
                    : c.LeagueTags.HasFlag(DnaTag.UnderGoals) ? "Giải thực dụng: thiên hướng Tài thẻ" : null,
                input.League.CardsFromProfile ? ProfileText : null,
                input.League.CardsKnown ? null : NoLeagueStatsText),
            HomeText = c.HomeForm.CardsFor is { } hc ? $"Nhận {Num(hc)} / Đối thủ {Num(c.HomeForm.CardsAgainst ?? 0)}" : "—",
            AwayText = c.AwayForm.CardsFor is { } ac ? $"Nhận {Num(ac)} / Đối thủ {Num(c.AwayForm.CardsAgainst ?? 0)}" : "—",
            Expected = expCards,
            MarketLine = line,
            MarketText = MoveText(input.Cards),
            Delta = delta,
            Lean = lean,
            Verdict = verdict,
        };
    }

    private static bool CornerStyleOver(Ctx c, out string why)
    {
        var reasons = new List<string>();
        if (c.HomeTags.HasFlag(DnaTag.OverCorners))
        {
            reasons.Add($"{c.Input.HomeName} cày góc");
        }

        if (c.AwayTags.HasFlag(DnaTag.OverCorners))
        {
            reasons.Add($"{c.Input.AwayName} cày góc");
        }

        if (c.LeagueTags.HasFlag(DnaTag.OverCorners))
        {
            reasons.Add("giải thiên hướng Tài góc");
        }

        var pressing = new[] { (c.Input.HomeName, c.HomeForm), (c.Input.AwayName, c.AwayForm) }
            .Where(t => t.Item2.ShotsFor >= 14 || t.Item2.CornersFor >= 6.5)
            .Select(t => $"{t.Item1} ép sân (sút {Num(t.Item2.ShotsFor)}, góc {Num(t.Item2.CornersFor)}/trận)");
        reasons.AddRange(pressing);
        why = string.Join(", ", reasons);
        return reasons.Count > 0;
    }

    private static bool CornerStyleUnder(Ctx c, out string why)
    {
        why = "";
        if (c.HomeTags.HasFlag(DnaTag.OverCorners) || c.AwayTags.HasFlag(DnaTag.OverCorners))
        {
            return false;
        }

        var reasons = new List<string>();
        if (c.LeagueTags.HasFlag(DnaTag.UnderCorners))
        {
            reasons.Add("giải thiên hướng Xỉu góc");
        }

        if (c.HomeTags.HasFlag(DnaTag.Pragmatic) || c.AwayTags.HasFlag(DnaTag.Pragmatic))
        {
            reasons.Add("có đội thực dụng/phá lối chơi");
        }

        if (c.HomeForm.CornersFor <= 5 && c.AwayForm.CornersFor <= 5)
        {
            reasons.Add("cả hai đội ít tạo góc (≤ 5/trận)");
        }

        why = string.Join(", ", reasons);
        return reasons.Count > 0;
    }

    // ---------------------------------------------------------------- Cờ đỏ

    private static List<RedFlag> RedFlags(Ctx c, HandicapModel h, double expGoals)
    {
        var input = c.Input;
        var flags = new List<RedFlag>();
        (Market, PickSide?)[] allMarkets =
        [
            (Market.AsianHandicap, null), (Market.OverUnder, null), (Market.CornersOverUnder, null),
            (Market.CornersHandicap, null), (Market.CardsOverUnder, null),
        ];

        // Cờ 1: đội "khôn nhà dại chợ" làm khách mà phải chấp 1/4 – 3/4.
        if (h.HMarket is { } line && line >= 0.25m && line <= 0.75m && c.AwayTags.HasFlag(DnaTag.AwayFragile))
        {
            flags.Add(new RedFlag(1, "Bẫy danh tiếng sân khách",
                $"{input.AwayName} (mạnh nhà – yếu khách) phải chấp {LineText.Handicap(line)} trên sân đối phương → loại cửa trên sân khách và Xỉu bàn trước trận.",
                [(Market.AsianHandicap, PickSide.Away), (Market.OverUnder, PickSide.Under)]));
        }

        // Cờ 2: xung đột thị trường – dữ liệu.
        var conflicts = new List<string>();
        var lineMoved = h.LineMove is { } lm && Math.Abs(lm) >= AnomalyMove;
        if (h.Verdict == HandicapVerdict.SuspectedTrap && h.IsAnomaly && (lineMoved || h.Scenario == HandicapScenario.ShallowLineForBetterHome))
        {
            conflicts.Add(h.Scenario == HandicapScenario.ShallowLineForBetterHome
                ? "kèo thơm ảo cho đội xếp trên, không có lý do lực lượng"
                : $"kèo chấp lệch {Signed(h.Delta ?? 0)} so với dự phóng mà không có lý do chấn thương/đội hình");
        }
        else if (h.Verdict == HandicapVerdict.SuspectedTrap && h.IsAnomaly && h.Delta is { } dl && dl != 0)
        {
            // Lệch dự phóng nhưng nhà cái không dịch kèo: chỉ nghi cửa nhà cái đang nâng; cửa còn lại và tài xỉu vẫn xét (tối đa B).
            var trap = dl < 0 ? PickSide.Home : PickSide.Away;
            var trapName = trap == PickSide.Home ? input.HomeName : input.AwayName;
            flags.Add(new RedFlag(2, "Kèo lạ — nghi cửa được nâng",
                $"Nhà cái {Sentence(h.HMarket!.Value, input)} lệch {Signed(dl)} so với dự phóng {Sentence(h.HExp, input)} mà kèo không dịch, không thấy lý do lực lượng → không chọn {trapName} trước trận.",
                [(Market.AsianHandicap, trap)]));
            c.HandicapUnreliable ??= $"Kèo lạ chưa rõ nguyên nhân (lệch {Signed(dl)})";
        }
        else if (h.Verdict == HandicapVerdict.SuspectedTrap && h.HMarket is { } trapLine)
        {
            // Kịch bản A/B nhưng độ lệch chưa tới 0.5: chỉ chặn cửa trên "ảo".
            var favourite = h.Scenario == HandicapScenario.ShallowLineForBetterHome || trapLine < 0 ? PickSide.Home : PickSide.Away;
            var name = favourite == PickSide.Home ? input.HomeName : input.AwayName;
            flags.Add(new RedFlag(2, h.Scenario == HandicapScenario.ShallowLineForBetterHome ? "Kèo thơm ảo" : "Bẫy dụ cửa trên",
                $"{h.Summary} → không chọn {name} trước trận.",
                [(Market.AsianHandicap, favourite)]));
        }

        if (h.LineMove is { } mv && Math.Abs(mv) >= AnomalyMove && h.HOpen is { } open)
        {
            var modelDir = Math.Sign(h.HExp - open);
            if (Math.Sign(mv) != modelDir && Causes(c, mv < 0).Count == 0)
            {
                conflicts.Add($"kèo chấp nhảy {Signed(mv)} từ lúc mở, ngược với phong độ");
            }
        }

        if (input.Totals is { } totals)
        {
            var tMove = totals.LineDelta;
            var formDir = Math.Sign(expGoals - (double)totals.Open.Line);
            if (Math.Abs(tMove) >= AnomalyMove && Math.Sign(tMove) != formDir)
            {
                conflicts.Add($"mốc tài xỉu {(tMove > 0 ? "tăng" : "giảm")} {Num((double)Math.Abs(tMove))} từ lúc mở, ngược với thống kê (kỳ vọng {Num(expGoals)})");
            }

            var bothOver = (c.LeagueTags.HasFlag(DnaTag.OverGoals) || c.HomeTags.HasFlag(DnaTag.OverGoals))
                && (c.LeagueTags.HasFlag(DnaTag.OverGoals) || c.AwayTags.HasFlag(DnaTag.OverGoals));
            if (bothOver && (double)totals.Current.Line <= Math.Min(input.League.Goals, expGoals) - 0.75)
            {
                conflicts.Add($"hai đội DNA Tài nhưng mốc tài xỉu chỉ {Num((double)totals.Current.Line)} (thấp bất thường)");
            }
        }

        if (conflicts.Count > 0)
        {
            flags.Add(new RedFlag(2, "Xung đột tín hiệu / kèo lạ",
                $"{Capitalize(string.Join("; ", conflicts))} → loại kèo chấp và bàn thắng trước trận (không áp cho kèo góc).",
                [(Market.AsianHandicap, null), (Market.OverUnder, null)]));
        }

        // Cờ 3: trận bế tắc.
        if (c.HomeForm.Matches >= MinSample && c.AwayForm.Matches >= MinSample
            && c.HomeForm.DrawRate >= 0.35 && c.AwayForm.DrawRate >= 0.35
            && Attack(c.HomeForm) < 0.9 && Attack(c.AwayForm) < 0.9
            && !(c.LeagueTags.HasFlag(DnaTag.HomeFortress) && h.HMarket is >= -0.5m and <= -0.25m))
        {
            flags.Add(new RedFlag(3, "Trận bế tắc (dead-lock)",
                $"Hai đội hòa nhiều ({Pct(c.HomeForm.DrawRate)} / {Pct(c.AwayForm.DrawRate)}) và tấn công yếu ({Num(Attack(c.HomeForm))} / {Num(Attack(c.AwayForm))} bàn hoặc xG mỗi trận) → bỏ kèo chấp.",
                [(Market.AsianHandicap, null)]));
        }

        // Cờ 4: động lực ảo / xoay tua.
        if (MotivationRisk(input) is { } reason)
        {
            flags.Add(new RedFlag(4, "Động lực ảo / xoay tua", $"{reason} → loại trước trận.", allMarkets));
        }

        // Cờ 5: 1–3 trận đầu của HLV mới / tân binh.
        foreach (var (name, regime) in new[] { (input.HomeName, c.HomeRegime), (input.AwayName, c.AwayRegime) })
        {
            if (regime is { InBuffer: true })
            {
                flags.Add(new RedFlag(5, "Chờ ổn định 2–3 trận",
                    $"{name}: {regime.KindText} {regime.PersonName} (từ {regime.StartDate:dd/MM/yyyy}), đây là trận thứ {regime.UpcomingMatchNumber} — hệ thống đang lắp ghép → bỏ qua kèo trước trận, chỉ theo dõi In-play sau phút 20.",
                    allMarkets));
            }
        }

        return flags;
    }

    private static double Attack(TeamForm form) => form.XgMatches >= MinSample && form.XgFor is { } xg ? xg : form.GoalsFor;

    /// <summary>Đội có ≥ <see cref="MinSample"/> trận có xG thì số bàn kỳ vọng trộn xG.</summary>
    public static bool UsesXg(TeamForm form) => form.XgMatches >= MinSample && form.XgFor.HasValue && form.XgAgainst.HasValue;

    private static double ScoringRate(TeamForm form) =>
        UsesXg(form) ? (1 - XgWeight) * form.GoalsFor + XgWeight * form.XgFor!.Value : form.GoalsFor;

    private static double ConcedingRate(TeamForm form) =>
        UsesXg(form) ? (1 - XgWeight) * form.GoalsAgainst + XgWeight * form.XgAgainst!.Value : form.GoalsAgainst;

    private static string XgText(TeamForm form) =>
        form is { XgMatches: > 0, XgFor: { } xf, XgAgainst: { } xa } ? $" · xG {Num(xf)}/{Num(xa)} ({form.XgMatches} trận)" : "";

    private static string XgNote(Ctx c) => (UsesXg(c.HomeForm), UsesXg(c.AwayForm)) switch
    {
        (true, true) => $" · kỳ vọng = {Pct(1 - XgWeight)} bàn thật + {Pct(XgWeight)} xG",
        (true, false) => $" · kỳ vọng trộn {Pct(XgWeight)} xG cho {c.Input.HomeName} ({c.Input.AwayName} chưa đủ {MinSample} trận có xG)",
        (false, true) => $" · kỳ vọng trộn {Pct(XgWeight)} xG cho {c.Input.AwayName} ({c.Input.HomeName} chưa đủ {MinSample} trận có xG)",
        _ => "",
    };

    /// <summary>Vòng đấu dễ có động lực ảo: 2 lượt cuối league phase cúp châu Âu, vòng loại, giao hữu.</summary>
    public static string? MotivationRisk(AnalysisInput input)
    {
        var round = input.Fixture.Round ?? "";
        var leagueName = input.LeagueName ?? "";
        if (leagueName.Contains("Friendl", StringComparison.OrdinalIgnoreCase))
        {
            return "Trận giao hữu";
        }

        if (round.Contains("Qualifying", StringComparison.OrdinalIgnoreCase) || round.Contains("Preliminary", StringComparison.OrdinalIgnoreCase))
        {
            return $"Vòng loại / vòng ngoài ({round})";
        }

        // UEFA: "League Stage - 7"; AFC Champions League Elite: "League Phase - 7" (đều 8 lượt).
        if ((round.StartsWith("League Stage", StringComparison.OrdinalIgnoreCase) || round.StartsWith("League Phase", StringComparison.OrdinalIgnoreCase))
            && int.TryParse(round.Split('-').Last().Trim(), out var md) && md >= 7)
        {
            return $"Lượt {md}/8 vòng phân hạng cúp châu lục: đội đã an bài dễ xoay tua";
        }

        if (round.StartsWith("Group", StringComparison.OrdinalIgnoreCase) && round.TrimEnd().EndsWith("- 6", StringComparison.Ordinal))
        {
            return "Lượt cuối vòng bảng";
        }

        return null;
    }

    // ---------------------------------------------------------------- Khuyến nghị

    private static void AddHandicapPick(List<AnalysisPickResult> picks, Ctx c, HandicapModel h, (double[] Diff, int Offset) goalDiff)
    {
        var market = c.Input.Handicap;
        if (market is null || h.Delta is not { } delta)
        {
            return;
        }

        var line = market.Current.Line;
        var trustMarket = h.Verdict == HandicapVerdict.RealStrength && h.IsAnomaly;
        var diff = goalDiff;
        if (trustMarket)
        {
            // Kèo thực lực: thị trường đã tính lực lượng mà mô hình chưa thấy → dùng độ vượt trội theo kèo.
            var lh = Math.Max(0.15, (c.ExpGoalsForTrust + (double)-line) / 2);
            var la = Math.Max(0.15, (c.ExpGoalsForTrust - (double)-line) / 2);
            diff = Distributions.Difference(Distributions.Poisson(lh, Distributions.MaxGoals), Distributions.Poisson(la, Distributions.MaxGoals));
        }

        var home = Distributions.Handicap(diff, PickSide.Home, line);
        var away = Distributions.Handicap(diff, PickSide.Away, -line);
        PickSide side;
        if (trustMarket)
        {
            side = delta < 0 ? PickSide.Home : delta > 0 ? PickSide.Away : line <= 0 ? PickSide.Home : PickSide.Away;
        }
        else if (delta != 0)
        {
            side = delta > 0 ? PickSide.Home : PickSide.Away;
        }
        else
        {
            side = home.ExpectedValue(market.Current.PriceA) >= away.ExpectedValue(market.Current.PriceB) ? PickSide.Home : PickSide.Away;
        }

        var isHome = side == PickSide.Home;
        var pick = new AnalysisPickResult
        {
            Market = Market.AsianHandicap,
            Side = side,
            Line = isHome ? line : -line,
            Odds = isHome ? market.Current.PriceA : market.Current.PriceB,
            Probability = isHome ? home : away,
            MarketShare = isHome ? PickProbability.NoVig(market.Current.PriceA, market.Current.PriceB) : PickProbability.NoVig(market.Current.PriceB, market.Current.PriceA),
            MeetsRule = !trustMarket && Math.Abs(delta) >= 0.25m,
            Label = $"{(isHome ? c.Input.HomeName : c.Input.AwayName)} {Signed(isHome ? line : -line)}",
        };

        pick.Reasons.Add(trustMarket
            ? $"Kèo thực lực: {string.Join("; ", h.Causes)}. Chỉ vào khi đội hình ra sân xác nhận."
            : $"Dự phóng {Signed(h.HExp)} vs nhà cái {Signed(line)} (Δ_H = {Signed(delta)}){(pick.MeetsRule ? " → giá trị nghiêng về cửa này" : "")}");
        picks.Add(pick);
        c.TrustMarketHandicap = trustMarket;
    }

    private static void AddTotalsPick(List<AnalysisPickResult> picks, Ctx c, Market market, MarketMovement? movement, double[] total, LayerRow row, string unit)
    {
        if (movement is null)
        {
            return;
        }

        var line = movement.Current.Line;
        var over = Distributions.Totals(total, PickSide.Over, line);
        var under = Distributions.Totals(total, PickSide.Under, line);
        var side = row.Lean
            ?? (over.ExpectedValue(movement.Current.PriceA) >= under.ExpectedValue(movement.Current.PriceB) ? PickSide.Over : PickSide.Under);
        var isOver = side == PickSide.Over;
        var pick = new AnalysisPickResult
        {
            Market = market,
            Side = side,
            Line = line,
            Odds = isOver ? movement.Current.PriceA : movement.Current.PriceB,
            Probability = isOver ? over : under,
            MarketShare = isOver ? PickProbability.NoVig(movement.Current.PriceA, movement.Current.PriceB) : PickProbability.NoVig(movement.Current.PriceB, movement.Current.PriceA),
            MeetsRule = row.Lean == side,
            Label = $"{(isOver ? "Tài" : "Xỉu")} {Num((double)line)} {unit}",
        };

        pick.Reasons.Add(row.Lean == side
            ? row.Verdict
            : $"Kỳ vọng {Num(row.Expected ?? 0)} vs mốc {Num((double)line)} (Δ = {Signed(row.Delta ?? 0)}): chưa đạt ngưỡng khuyến nghị");
        picks.Add(pick);
    }

    private static void AddCornerHandicapPick(List<AnalysisPickResult> picks, Ctx c, (double[] Diff, int Offset) diff, LayerRow row)
    {
        var movement = c.Input.CornersHandicap;
        if (movement is null)
        {
            return;
        }

        var line = movement.Current.Line;
        var home = Distributions.Handicap(diff, PickSide.Home, line);
        var away = Distributions.Handicap(diff, PickSide.Away, -line);
        var side = row.Lean
            ?? (home.ExpectedValue(movement.Current.PriceA) >= away.ExpectedValue(movement.Current.PriceB) ? PickSide.Home : PickSide.Away);
        var isHome = side == PickSide.Home;
        var pick = new AnalysisPickResult
        {
            Market = Market.CornersHandicap,
            Side = side,
            Line = isHome ? line : -line,
            Odds = isHome ? movement.Current.PriceA : movement.Current.PriceB,
            Probability = isHome ? home : away,
            MarketShare = isHome ? PickProbability.NoVig(movement.Current.PriceA, movement.Current.PriceB) : PickProbability.NoVig(movement.Current.PriceB, movement.Current.PriceA),
            MeetsRule = row.Lean == side,
            Label = $"{(isHome ? c.Input.HomeName : c.Input.AwayName)} {Signed(isHome ? line : -line)} góc",
        };

        pick.Reasons.Add(row.Lean == side ? row.Verdict : $"Chênh góc kiếm {Signed(row.Expected ?? 0)}/trận: chưa đạt ngưỡng 2.0 so với mức chấp");
        picks.Add(pick);
    }

    // ---------------------------------------------------------------- Chấm điểm

    private static void Grade(AnalysisPickResult pick, Ctx c, IReadOnlyList<RedFlag> flags)
    {
        var sample = pick.Market is Market.CornersOverUnder or Market.CornersHandicap
            ? Math.Min(c.HomeForm.CornerMatches, c.AwayForm.CornerMatches)
            : Math.Min(c.HomeForm.Matches, c.AwayForm.Matches);
        pick.Sample = sample;
        var lowSample = sample < MinSample;

        var model = pick.Probability;
        pick.ModelProbability = model;
        pick.Reasons.Insert(0, $"Mô hình: thắng {Pct(model.Win)}{(model.HalfWin > 0.005 ? $" + nửa {Pct(model.HalfWin)}" : "")}, hoàn {Pct(model.Push)}, thua {Pct(model.Loss)}{(model.HalfLoss > 0.005 ? $" + nửa {Pct(model.HalfLoss)}" : "")} · EV {Signed(model.ExpectedValue(pick.Odds))}/1 đơn vị @{LineText.Odds(pick.Odds)}");

        // Trộn với giá nhà cái: mô hình chỉ thấy vài chục trận, thị trường thấy cả lực lượng, tin tức, dòng tiền.
        double? divergence = null;
        if (pick.MarketShare is { } q && model.Share is { } share)
        {
            var w = ModelWeight(sample);
            var blended = BlendedShare(share, q, sample);
            pick.Probability = model.WithShare(blended);
            divergence = share - q;
            pick.Reasons.Insert(1, $"Giá nhà cái (đã bỏ lời) cho cửa này {Pct(q)}, mô hình {Pct(share)} → dùng {Pct(blended)} (tin mô hình {Pct(w)} vì mẫu {sample} trận) · EV {Signed(pick.ExpectedValue)}");
        }

        var p = pick.Probability;
        var ev = pick.ExpectedValue;
        var score = 5.0 + Math.Clamp(6 * p.Net, -2.5, 2.5) + Math.Clamp(8 * ev, -1.5, 1.5);

        if (pick.MeetsRule)
        {
            score += 0.75;
        }

        var (dna, dnaText) = DnaBonus(pick, c);
        if (dna != 0)
        {
            score += dna;
            pick.Reasons.Add($"DNA {(dna > 0 ? "cùng hướng" : "ngược hướng")} ({Signed(dna)}): {dnaText}");
        }

        foreach (var (name, regime) in new[] { (c.Input.HomeName, c.HomeRegime), (c.Input.AwayName, c.AwayRegime) })
        {
            if (regime is { InBuffer: false, UsesNewSample: true })
            {
                score += 0.5;
                pick.Reasons.Add($"{name} đã qua 3 trận ổn định với {regime.KindText} {regime.PersonName}: dùng dữ liệu giai đoạn mới (+0.5)");
                break;
            }
        }

        if (pick.Market is Market.AsianHandicap or Market.OverUnder)
        {
            if (ApiAgrees(pick, c))
            {
                if (pick.Market == Market.AsianHandicap && pick.Line >= 1m)
                {
                    pick.Reasons.Add("Dự đoán API chọn đội được chấp từ 1 trái trở lên thắng — trái hẳn kèo nhà cái, không cộng điểm");
                }
                else
                {
                    score += 0.25;
                    pick.Reasons.Add("Dự đoán API cùng hướng (+0.25)");
                }
            }

            if (c.Input.RuleSides.Contains(pick.Side))
            {
                score += 0.5;
                pick.Reasons.Add("Quy tắc của bạn cũng chọn cửa này (+0.5)");
            }

            var movement = pick.Market == Market.AsianHandicap ? c.Input.Handicap : c.Input.Totals;
            if (movement is not null && movement.LeanFor(pick.Side) == Lean.Toward)
            {
                score += 0.25;
                pick.Reasons.Add("Kèo đang dịch về cửa này (+0.25)");
            }
        }

        if (lowSample)
        {
            score -= 1.0;
            pick.Reasons.Add($"Mẫu dữ liệu ít ({sample} trận): giảm độ tin cậy (−1), tối đa nhóm B");
        }
        else if (sample < 8)
        {
            score -= 0.5;
            pick.Reasons.Add($"Mẫu còn ngắn ({sample} trận): giảm nhẹ độ tin cậy (−0.5)");
        }

        var capB = false;
        if (pick.Market == Market.CardsOverUnder)
        {
            capB = true;
            pick.Reasons.Add("Kèo thẻ chỉ để tham khảo (còn phụ thuộc trọng tài, tính chất trận): tối đa nhóm B");
        }

        if (pick.Market == Market.OverUnder && pick.Side == PickSide.Over && pick.Line >= 3.25m
            && (c.LeagueTags.HasFlag(DnaTag.OverGoals) || c.HomeTags.HasFlag(DnaTag.OverGoals) || c.AwayTags.HasFlag(DnaTag.OverGoals)))
        {
            capB = true;
            pick.Reasons.Add("Mốc Tài đã bị đẩy cao (≥ 3.25): chờ hạ nhịp In-play thay vì vào trước trận");
        }

        if (pick.Market == Market.AsianHandicap && c.TrustMarketHandicap)
        {
            capB = true;
            score = Math.Max(score, 6.0);
        }

        if (divergence is { } dv && Math.Abs(dv) >= MarketDivergenceCap && sample < ReliableSample)
        {
            capB = true;
            pick.Reasons.Add($"Mô hình lệch giá nhà cái {Pct(Math.Abs(dv))} mà mẫu chỉ {sample} trận: dễ là mô hình sai hơn là nhà cái sai → tối đa nhóm B");
        }

        if (pick.Market == Market.AsianHandicap && c.HandicapUnreliable is { } why)
        {
            capB = true;
            pick.Reasons.Add($"{why} → kèo chấp tối đa nhóm B");
        }

        score = Math.Round(Math.Clamp(score, 0, 10), 1);
        if (capB)
        {
            score = Math.Min(score, 7.5);
        }
        else if (!pick.MeetsRule || lowSample)
        {
            // Chưa đạt ngưỡng khuyến nghị của file kỹ năng, hoặc mẫu quá ít: không vượt ngưỡng nhóm A dù giá đẹp.
            score = Math.Min(score, 7.9);
        }

        if (ev < 0 && pick.MarketShare is not null && score >= 6)
        {
            score = 5.9;
            pick.Reasons.Add($"Sau khi tính giá nhà cái, EV âm ({Signed(ev)}): không vào");
        }

        pick.Score = score;
        foreach (var flag in flags.Where(f => f.Covers(pick.Market, pick.Side)))
        {
            pick.Flags.Add(flag.Number);
        }

        if (pick.Flags.Count > 0)
        {
            pick.Group = AnalysisGroup.C;
            // Điểm hiển thị phải khớp với việc bị loại: không để "9.9 điểm mà nhóm C".
            pick.Score = Math.Min(score, 5.9);
            pick.Reasons.Add($"Dính cờ đỏ {string.Join(", ", pick.Flags.Distinct())}: loại trước trận{(score > 5.9 ? $" (điểm trước cờ đỏ {score:0.0})" : "")}");
        }
        else if (score >= 8 && pick.MeetsRule && dna >= 0 && !capB && !lowSample)
        {
            pick.Group = AnalysisGroup.A;
        }
        else if (score >= 6)
        {
            pick.Group = AnalysisGroup.B;
        }
        else
        {
            pick.Group = AnalysisGroup.C;
        }
    }

    private static (double Bonus, string Text) DnaBonus(AnalysisPickResult pick, Ctx c)
    {
        var input = c.Input;
        var parts = new List<(double, string)>();
        void Add(bool when, double value, string text)
        {
            if (when)
            {
                parts.Add((value, text));
            }
        }

        var league = input.LeagueDna?.LeagueName ?? input.LeagueName ?? "giải";
        var anyTeamOver = c.HomeTags.HasFlag(DnaTag.OverGoals) || c.AwayTags.HasFlag(DnaTag.OverGoals);
        var anyPragmatic = c.HomeTags.HasFlag(DnaTag.Pragmatic) || c.AwayTags.HasFlag(DnaTag.Pragmatic);
        var anyCornerTeam = c.HomeTags.HasFlag(DnaTag.OverCorners) || c.AwayTags.HasFlag(DnaTag.OverCorners);
        bool Any(DnaTag tag) => c.HomeTags.HasFlag(tag) || c.AwayTags.HasFlag(tag);
        string Who(DnaTag tag) => c.HomeTags.HasFlag(tag) && c.AwayTags.HasFlag(tag) ? "cả hai đội" : c.HomeTags.HasFlag(tag) ? input.HomeName : input.AwayName;

        // DNA giải cho kèo chấp; pick.Line là mức của cửa được chọn (âm = chấp, dương = được chấp).
        void LeagueHandicap()
        {
            Add(c.LeagueTags.HasFlag(DnaTag.UnderdogCover) && pick.Line > 0m, 0.5, $"{league} – trình độ đồng đều, cửa dưới hay ăn");
            Add(c.LeagueTags.HasFlag(DnaTag.UnderdogCover) && pick.Line <= -0.75m, -0.5, $"{league} – cửa dưới hay ăn (kỵ chấp sâu)");
            Add(c.LeagueTags.HasFlag(DnaTag.BullyWeak) && pick.Line <= -1m, 0.5, $"{league} – phân hóa lớn, tín kèo trên chấp sâu");
            Add(c.LeagueTags.HasFlag(DnaTag.BullyWeak) && pick.Line >= 1m, -0.5, $"{league} – phân hóa lớn (kèo trên chấp sâu vẫn ăn)");
            Add(c.LeagueTags.HasFlag(DnaTag.Pragmatic) && pick.Line >= 0.25m, 0.25, $"{league} – vua hòa, thắng sát nút");
            Add(c.LeagueTags.HasFlag(DnaTag.Pragmatic) && pick.Line <= -0.75m, -0.25, $"{league} – vua hòa, khó thắng cách biệt");
        }

        // "Bắt nạt đội yếu": chỉ tính khi đội đó chấp từ 1 trái và không xếp dưới đối thủ trên BXH.
        bool HomeAbove() => input.HomeStanding?.Rank is not { } rh || input.AwayStanding?.Rank is not { } ra || rh < ra;
        bool AwayAbove() => input.HomeStanding?.Rank is not { } rh || input.AwayStanding?.Rank is not { } ra || ra < rh;
        switch (pick.Market, pick.Side)
        {
            case (Market.OverUnder, PickSide.Over):
                Add(c.LeagueTags.HasFlag(DnaTag.OverGoals), 0.5, $"{league} – giải Tài bàn");
                Add(anyTeamOver, 0.5, "có đội DNA nổ Tài");
                Add(Any(DnaTag.Btts), 0.25, $"{Who(DnaTag.Btts)} công làm thủ phá (BTTS)");
                Add(c.LeagueTags.HasFlag(DnaTag.UnderGoals), -0.5, $"{league} – giải Xỉu/thực dụng");
                Add(c.HomeTags.HasFlag(DnaTag.Pragmatic) && c.AwayTags.HasFlag(DnaTag.Pragmatic), -0.5, "hai đội thực dụng");
                Add(c.LeagueTags.HasFlag(DnaTag.Pragmatic), -0.25, $"{league} – vua hòa, thắng sát nút");
                break;
            case (Market.OverUnder, PickSide.Under):
                Add(c.LeagueTags.HasFlag(DnaTag.UnderGoals), 0.5, $"{league} – giải Xỉu/thực dụng");
                Add(anyPragmatic && !anyTeamOver, 0.5, "có đội thực dụng");
                Add(c.LeagueTags.HasFlag(DnaTag.OverGoals), -0.5, $"{league} – giải Tài bàn (hạn chế Xỉu)");
                Add(anyTeamOver, -0.5, "có đội DNA nổ Tài");
                Add(Any(DnaTag.Btts), -0.25, $"{Who(DnaTag.Btts)} công làm thủ phá (BTTS)");
                Add(c.LeagueTags.HasFlag(DnaTag.Pragmatic), 0.25, $"{league} – vua hòa, thắng sát nút");
                break;
            case (Market.AsianHandicap, PickSide.Home):
                Add(c.LeagueTags.HasFlag(DnaTag.HomeFortress) && pick.Line is >= -0.5m and <= 0m, 0.5, $"{league} – thánh địa sân nhà, chủ nhà chấp nhẹ");
                Add(c.LeagueTags.HasFlag(DnaTag.HomeFortress) && pick.Line is >= -0.5m and <= 0m && c.HomeForm.WinRate >= 0.5, 0.5, "chủ nhà phong độ ổn định");
                Add(c.HomeTags.HasFlag(DnaTag.HomeFortress), 0.5, $"{input.HomeName} mạnh sân nhà");
                Add(c.AwayTags.HasFlag(DnaTag.AwayFragile), 0.25, $"{input.AwayName} yếu sân khách");
                Add(c.HomeTags.HasFlag(DnaTag.BullyWeak) && pick.Line <= -1m && HomeAbove(), 0.5, $"{input.HomeName} bắt nạt đội yếu, thắng được kèo chấp sâu");
                Add(c.AwayTags.HasFlag(DnaTag.BullyWeak) && pick.Line >= 1m && AwayAbove(), -0.5, $"{input.AwayName} bắt nạt đội yếu (chấp sâu vẫn thắng)");
                LeagueHandicap();
                break;
            case (Market.AsianHandicap, PickSide.Away):
                Add(c.LeagueTags.HasFlag(DnaTag.HomeFortress), -0.5, $"{league} – thánh địa sân nhà (kỵ đội khách)");
                Add(c.HomeTags.HasFlag(DnaTag.HomeFortress), -0.5, $"{input.HomeName} mạnh sân nhà");
                Add(c.AwayTags.HasFlag(DnaTag.AwayFragile), -0.5, $"{input.AwayName} yếu sân khách");
                Add(c.AwayTags.HasFlag(DnaTag.BullyWeak) && pick.Line <= -1m && AwayAbove(), 0.5, $"{input.AwayName} bắt nạt đội yếu, thắng được kèo chấp sâu");
                Add(c.HomeTags.HasFlag(DnaTag.BullyWeak) && pick.Line >= 1m && HomeAbove(), -0.5, $"{input.HomeName} bắt nạt đội yếu (chấp sâu vẫn thắng)");
                LeagueHandicap();
                break;
            case (Market.CornersOverUnder, PickSide.Over):
                Add(c.LeagueTags.HasFlag(DnaTag.OverCorners), 0.5, $"{league} – giải Tài góc");
                Add(anyCornerTeam, 0.5, "có đội cày góc");
                Add(Any(DnaTag.ConcedesCorners), 0.25, $"{Who(DnaTag.ConcedesCorners)} hay xả góc / chịu góc");
                Add(c.LeagueTags.HasFlag(DnaTag.UnderCorners), -0.5, $"{league} – giải Xỉu góc");
                break;
            case (Market.CornersOverUnder, PickSide.Under):
                Add(c.LeagueTags.HasFlag(DnaTag.UnderCorners), 0.5, $"{league} – giải Xỉu góc");
                Add(anyPragmatic, 0.25, "có đội thực dụng");
                Add(c.LeagueTags.HasFlag(DnaTag.OverCorners), -0.5, $"{league} – giải Tài góc");
                Add(anyCornerTeam, -0.5, "có đội cày góc");
                Add(Any(DnaTag.ConcedesCorners), -0.25, $"{Who(DnaTag.ConcedesCorners)} hay xả góc / chịu góc");
                break;
            case (Market.CornersHandicap, PickSide.Home):
                Add(c.HomeTags.HasFlag(DnaTag.OverCorners), 0.5, $"{input.HomeName} cày góc");
                Add(c.HomeTags.HasFlag(DnaTag.HomeFortress) || c.LeagueTags.HasFlag(DnaTag.HomeFortress), 0.5, "chấp góc chủ nhà ở sân mạnh");
                Add(c.AwayTags.HasFlag(DnaTag.ConcedesCorners), 0.25, $"{input.AwayName} hay chịu góc");
                break;
            case (Market.CornersHandicap, PickSide.Away):
                Add(c.AwayTags.HasFlag(DnaTag.OverCorners), 0.5, $"{input.AwayName} cày góc");
                Add(c.HomeTags.HasFlag(DnaTag.HomeFortress), -0.5, $"{input.HomeName} mạnh sân nhà");
                Add(c.HomeTags.HasFlag(DnaTag.ConcedesCorners), 0.25, $"{input.HomeName} hay chịu góc");
                break;
            case (Market.CardsOverUnder, PickSide.Over):
                Add(c.LeagueTags.HasFlag(DnaTag.HeavyCards), 0.5, $"{league} – giải mưa thẻ");
                Add(c.LeagueTags.HasFlag(DnaTag.UnderGoals) && !c.LeagueTags.HasFlag(DnaTag.HeavyCards), 0.5, $"{league} – giải thực dụng (Tài thẻ)");
                Add(Any(DnaTag.HeavyCards), 0.5, $"{Who(DnaTag.HeavyCards)} đá rát, nhiều thẻ");
                break;
            case (Market.CardsOverUnder, PickSide.Under):
                Add(c.LeagueTags.HasFlag(DnaTag.HeavyCards), -0.5, $"{league} – giải mưa thẻ");
                Add(Any(DnaTag.HeavyCards), -0.5, $"{Who(DnaTag.HeavyCards)} đá rát, nhiều thẻ");
                break;
        }

        var bonus = Math.Clamp(parts.Sum(x => x.Item1), -1.0, 1.0);
        return (bonus, string.Join("; ", parts.Select(x => x.Item2)));
    }

    private static bool ApiAgrees(AnalysisPickResult pick, Ctx c)
    {
        var advice = c.Input.Advice;
        if (advice is null)
        {
            return false;
        }

        return pick.Side switch
        {
            PickSide.Home => advice.WinnerTeamId == c.Input.Fixture.HomeTeamId,
            PickSide.Away => advice.WinnerTeamId == c.Input.Fixture.AwayTeamId,
            _ => advice.UnderOverSide == pick.Side,
        };
    }

    // ---------------------------------------------------------------- In-play (kịch bản trước trận)

    private static List<string> InPlayHints(Ctx c, HandicapModel h, LayerRow corners)
    {
        var input = c.Input;
        var hints = new List<string>();
        var overDna = c.LeagueTags.HasFlag(DnaTag.OverGoals) || c.HomeTags.HasFlag(DnaTag.OverGoals) || c.AwayTags.HasFlag(DnaTag.OverGoals);
        if (overDna)
        {
            hints.Add("Có bàn trong 15 phút đầu → hủy mọi nhận định Xỉu, chuyển sang săn Tài bàn tiếp theo (pha rượt đuổi).");
        }

        if (overDna && input.Totals?.Current.Line >= 3.25m)
        {
            hints.Add($"Mốc Tài trước trận cao ({Num((double)input.Totals!.Current.Line)}): nếu 15–20' chưa có bàn mà sút liên tục → vào Tài live khi mốc về 2.5–2.75 (hoặc Tài 0.5 hiệp 1).");
        }

        foreach (var (name, regime) in new[] { (input.HomeName, c.HomeRegime), (input.AwayName, c.AwayRegime) })
        {
            if (regime is { InBuffer: true })
            {
                hints.Add($"{name} ({regime.KindText}, trận thứ {regime.UpcomingMatchNumber}): quan sát 20–25' đầu; chỉ vào lệnh nếu số cú sút/tấn công nguy hiểm áp đảo đúng triết lý mới.");
            }
        }

        if (h.HMarket is { } line && line != 0)
        {
            var favourite = line < 0 ? input.HomeName : input.AwayName;
            hints.Add($"Nếu hiệp 1 hòa 0-0 và tổng sút trúng đích ≤ 1: không theo {favourite} ở hiệp 2; ưu tiên cửa dưới / Xỉu in-play{(corners.Lean == PickSide.Over ? ", hoặc chuyển sang kèo góc nếu cửa trên chỉ tạt cánh" : "")}.");
        }

        if (c.LeagueTags.HasFlag(DnaTag.LateGoals))
        {
            hints.Add("Giải hay có bàn muộn"
                + (input.LeagueDna?.LateGoalPct is { } late ? $" ({Num((double)late)}% trận có bàn sau phút 75)" : "")
                + ": thể lực hàng thủ đuối từ phút 65–70 → săn bàn muộn từ phút 70"
                + (c.LeagueTags.HasFlag(DnaTag.HomeFortress) ? ", ưu tiên chủ nhà." : "."));
        }

        if (c.LeagueTags.HasFlag(DnaTag.FastStart))
        {
            hints.Add("Giải hay nổ sớm hiệp 1"
                + (input.LeagueDna?.FirstHalfGoalPct is { } h1 ? $" ({Num((double)h1)}% trận có bàn H1)" : "")
                + ": nếu 15' đầu hai đội sút nhiều mà chưa có bàn → cân nhắc Tài H1 0.5/0.75.");
        }

        foreach (var (name, tags) in new[] { (input.HomeName, c.HomeTags), (input.AwayName, c.AwayTags) })
        {
            if (tags.HasFlag(DnaTag.FastStart))
            {
                hints.Add($"{name} đánh phủ đầu (hay có bàn hiệp 1): theo dõi Tài H1 / bàn sớm trong 15–30' đầu.");
            }

            if (tags.HasFlag(DnaTag.LateGoals))
            {
                hints.Add($"{name} hay bùng nổ cuối trận: săn Tài In-play hiệp 2 từ phút 60–70 nếu tỉ số chưa an bài.");
            }
        }

        return hints;
    }

    // ---------------------------------------------------------------- Tiện ích

    /// <summary>Số ngày nghỉ từ trận trước; quá <see cref="MaxRestDays"/> ngày thường là CSDL thiếu trận (giải chưa theo dõi) nên coi như không rõ.</summary>
    private static double? RestDays(IReadOnlyList<TeamMatch> history, DateTime kickoff) =>
        history.FirstOrDefault(m => m.KickoffUtc < kickoff) is { } last && (kickoff - last.KickoffUtc).TotalDays is var days && days <= MaxRestDays ? days : null;

    public const int MaxRestDays = 21;

    private const string ProfileText = "TB giải theo hồ sơ 1 năm (CSDL chưa đủ 20 trận)";

    private const string NoLeagueStatsText = "chưa có số liệu của giải (API không có thống kê hoặc chưa tải): tạm dùng số mặc định";

    private static string Hint(params string?[] parts) => string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));

    /// <summary>Tỉ trọng tin mô hình khi trộn với giá nhà cái, theo số trận mẫu.</summary>
    public static double ModelWeight(int sample) => Math.Clamp(ModelWeightMin + ModelWeightPerMatch * sample, ModelWeightMin, ModelWeightMax);

    /// <summary>Tỉ lệ ăn sau khi trộn tỉ lệ của mô hình với giá nhà cái (đã bỏ lời) theo <see cref="ModelWeight"/>.</summary>
    public static double BlendedShare(double modelShare, double marketShare, int sample) =>
        marketShare + ModelWeight(sample) * (modelShare - marketShare);

    /// <summary>Phân phối của mô hình sau khi trộn với giá nhà cái; không có giá thì giữ nguyên.</summary>
    public static PickProbability BlendWithMarket(PickProbability model, double? marketShare, int sample) =>
        marketShare is { } q && model.Share is { } share ? model.WithShare(BlendedShare(share, q, sample)) : model;

    private static double Shrink(double value, int n, double prior) =>
        n >= MinSample ? value : (value * n + prior * (MinSample - n)) / MinSample;

    /// <summary>Trộn 50/50 số liệu chung và số liệu theo sân khi có ≥ 3 trận theo sân.</summary>
    private static double? Blend(double? overall, double? venue, int venueMatches) =>
        overall is null ? venue : venue is null || venueMatches < 3 ? overall : (overall + venue) / 2;

    public static decimal RoundQuarter(double value) => Math.Round((decimal)value * 4, MidpointRounding.AwayFromZero) / 4;

    private static string DnaText(DnaTag tags, DnaTag over, DnaTag under) =>
        tags.HasFlag(over) ? DnaTagText.Label(over, false) : tags.HasFlag(under) ? DnaTagText.Label(under, false) : "";

    private static string MoveText(MarketMovement? m, string? note = null)
    {
        if (m is null)
        {
            return "—";
        }

        var text = m.LineDelta == 0 ? Num((double)m.Current.Line) : $"{Num((double)m.Open.Line)} → {Num((double)m.Current.Line)}";
        if (Math.Abs(m.LineDelta) >= AnomalyMove)
        {
            text += " (nhảy ≥ 0.5)";
        }

        return note is null ? text : $"{text} · {note}";
    }

    private static string Num(double? value) => value is { } v ? v.ToString("0.##", Vi) : "—";

    private static string Signed(double value) => (value >= 0 ? "+" : "−") + Math.Abs(value).ToString("0.00", Vi);

    private static string Signed(decimal value) => LineText.Signed(value);

    private static string Pct(double value) => (value * 100).ToString("0", Vi) + "%";

    /// <summary>"Chelsea chấp 1/4 trái" hoặc "đồng banh" để đặt giữa câu.</summary>
    private static string Sentence(decimal homeLine, AnalysisInput input) =>
        homeLine == 0 ? "đồng banh" : LineText.HandicapSentence(homeLine, input.HomeName, input.AwayName);

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private sealed class Ctx(
        AnalysisInput input, DnaTag leagueTags, DnaTag homeTags, DnaTag awayTags,
        RegimeStatus? homeRegime, RegimeStatus? awayRegime, TeamForm homeForm, TeamForm awayForm,
        int homeMissing, int awayMissing, double? homeRest, double? awayRest)
    {
        public AnalysisInput Input { get; } = input;
        public DnaTag LeagueTags { get; } = leagueTags;
        public DnaTag HomeTags { get; } = homeTags;
        public DnaTag AwayTags { get; } = awayTags;
        public RegimeStatus? HomeRegime { get; } = homeRegime;
        public RegimeStatus? AwayRegime { get; } = awayRegime;
        public TeamForm HomeForm { get; } = homeForm;
        public TeamForm AwayForm { get; } = awayForm;
        public int HomeMissing { get; } = homeMissing;
        public int AwayMissing { get; } = awayMissing;
        public double? HomeRest { get; } = homeRest;
        public double? AwayRest { get; } = awayRest;

        /// <summary>Tổng bàn dùng khi tin theo kèo (kèo thực lực).</summary>
        public double ExpGoalsForTrust => Input.Totals is { } t ? (double)t.Current.Line : Input.League.Goals;

        public bool TrustMarketHandicap { get; set; }

        /// <summary>Lý do dự phóng kèo chấp kém tin cậy (mẫu ít, trận cúp, kèo lạ chưa rõ): kèo chấp tối đa nhóm B.</summary>
        public string? HandicapUnreliable { get; set; }
    }
}
