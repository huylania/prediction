using System.Text.Json;
using Prediction.Core.Engine.Analysis;
using Prediction.Core.Engine.LiveCorners;
using Prediction.Core.Entities;
using Prediction.Web.ApiFootball;
using Prediction.Web.Services;

namespace Prediction.Tests;

public class LiveCornerTests
{
    private static LiveCornerInput H1(int minute, int corners, int? shots = null, int? blocked = null, int? da = null, decimal? line = null,
        decimal? over = 1.85m, decimal? under = 1.85m, int hg = 0, int ag = 0) => new()
    {
        Window = CornerWindow.FirstHalf, Minute = minute, HomeCorners = corners, AwayCorners = 0, Shots = shots, BlockedShots = blocked,
        DangerousAttacks = da, Line = line, OverOdds = over, UnderOdds = under, HomeGoals = hg, AwayGoals = ag,
    };

    private static LiveCornerInput H2(int minute, int corners, int hg, int ag, decimal? line, int? h1 = null, PickSide? fav = null,
        decimal? over = 1.85m, decimal? under = 1.85m, Motivation moti = Motivation.Normal, int? recentShots = null, int? recentCorners = null,
        int? recentBlocked = null, int? shots = null, decimal? handicap = null, bool drawSuits = false, int homeRed = 0, int awayRed = 0) => new()
    {
        Window = CornerWindow.SecondHalf, Minute = minute, HomeCorners = corners, AwayCorners = 0, FirstHalfCorners = h1,
        HomeGoals = hg, AwayGoals = ag, Line = line, OverOdds = over, UnderOdds = under, Favourite = fav, Motivation = moti,
        Shots = shots, RecentMinutes = recentShots is null ? null : 15, RecentShots = recentShots, RecentCorners = recentCorners,
        RecentBlocked = recentBlocked, PreMatchHandicap = handicap, DrawSuitsBoth = drawSuits, HomeRed = homeRed, AwayRed = awayRed,
    };

    private static double Factor(LiveCornerResult r, string name) => r.Factors.Single(f => f.Name == name).Value;

    [Fact]
    public void First_half_regresses_rate_clamps_alpha_and_uses_47_minus_t()
    {
        // Phút 28, 4 góc, 9 cú sút (3 chặn) → α_sút 1.12; 30 TCNH/28' → 1.08; 0-0 ép sân → 1.05; tích 1.27 → kẹp 1.25.
        var r = LiveCornerAnalyzer.Analyze(H1(28, 4, shots: 9, blocked: 3, da: 30, line: 5.5m));
        Assert.Equal(4.0 / 28, r.RawRate, 6);
        Assert.Equal(0.55 * 4 / 28 + 0.45 * 0.105, r.Rate, 6);
        Assert.Equal(1.12 * 1.08 * 1.05, r.RawMultiplier, 6);
        Assert.Equal(1.25, r.Multiplier, 6);
        Assert.Equal(19, r.RemainingMinutes, 6);
        Assert.Equal(r.Rate * 1.25 * 19, r.ExpectedRemaining, 6);
        Assert.Equal(1.5, r.DLine!.Value, 6);
        Assert.Equal(CornerCall.Over, r.Call);
        Assert.True(r.WinRate > 0.72);
        Assert.InRange(r.Confidence, 9.0, 9.5);
        Assert.Contains("TÀI GÓC H1 5.5", r.Headline);

        // Cùng trận nhưng mốc 6.5 (cần thêm 2.5 góc) → bộ lọc chặn.
        var far = LiveCornerAnalyzer.Analyze(H1(28, 4, shots: 9, blocked: 3, da: 30, line: 6.5m));
        Assert.Equal(CornerCall.NoBet, far.Call);
        Assert.False(far.Filters.Single(f => f.Name == "Cần thêm ≤ 2 góc").Passed);
    }

    [Fact]
    public void First_half_early_burst_veto()
    {
        // Phút 27 đã 6 góc, mốc 7.5: tuyệt đối không Tài dù biên dương lớn.
        var r = LiveCornerAnalyzer.Analyze(H1(27, 6, shots: 8, blocked: 2, line: 7.5m));
        Assert.True(r.Edge > 0.6);
        Assert.Equal(CornerCall.NoBet, r.Call);
        Assert.False(r.Filters.Single(f => f.Name == "Cấm Tài mốc cao").Passed);
        Assert.Contains("bộ lọc sắt", r.Headline);
    }

    [Fact]
    public void First_half_under_needs_two_corner_cushion()
    {
        // Ít sút, nhịp chậm: α = 0.82 × 0.82 × 1 → kẹp 0.72.
        var ok = LiveCornerAnalyzer.Analyze(H1(28, 1, shots: 2, blocked: 0, line: 3.5m, under: 1.80m));
        Assert.Equal(0.72, ok.Multiplier, 6);
        Assert.Contains("chỉ số thay thế", ok.Factors[1].Reason);
        Assert.Equal(CornerCall.Under, ok.Call);
        Assert.Equal(PickSide.Under, ok.Side);

        var tight = LiveCornerAnalyzer.Analyze(H1(28, 1, shots: 2, blocked: 0, line: 2.5m, under: 1.80m));
        Assert.Equal(CornerCall.NoBet, tight.Call);
        Assert.False(tight.Filters.Single(f => f.Name == "Đệm Xỉu ≥ 2 góc").Passed);

        // Ngoại lệ: phút ≥ 28, 0 góc, ≤ 2 cú sút → cho phép Xỉu 1.5.
        var exception = LiveCornerAnalyzer.Analyze(H1(29, 0, shots: 1, blocked: 0, line: 1.5m, under: 1.80m));
        Assert.True(exception.Filters.Single(f => f.Name == "Đệm Xỉu ≥ 2 góc").Passed);
    }

    [Fact]
    public void Second_half_isolates_h2_tempo_with_floor_and_dead_h2()
    {
        // 9 góc phút 77 nhưng 8 góc là của hiệp 1: H2 gần như đứng hình.
        var r = LiveCornerAnalyzer.Analyze(H2(77, 9, 1, 1, 11m, h1: 8));
        Assert.Equal(1.0 / 32, r.SecondHalfRate!.Value, 6);
        Assert.Equal(LiveCornerAnalyzer.MinSecondHalfRate, r.Rate, 6); // 0.55·0.031 + 0.25·0.117 + 0.021 < 0.068 → sàn
        Assert.Equal(0.78, Factor(r, "K_cường độ"));
        Assert.Equal(18, r.RemainingMinutes, 6);

        var noH1 = LiveCornerAnalyzer.Analyze(H2(77, 9, 1, 1, 11m));
        Assert.Null(noH1.SecondHalfRate);
        Assert.Equal(0.5 * 9 / 77 + 0.5 * 0.105, noH1.Rate, 6);

        // ≥ 4 bàn: bù giờ 6 phút → ΔT = 96 − T.
        Assert.Equal(19, LiveCornerAnalyzer.Analyze(H2(77, 9, 3, 2, 11m)).RemainingMinutes, 6);
    }

    [Fact]
    public void Second_half_score_matrix_four_levels()
    {
        Assert.Equal(0.60, Factor(LiveCornerAnalyzer.Analyze(H2(76, 8, 3, 0, 9m)), "K_tỉ số"));
        Assert.Equal(1.20, Factor(LiveCornerAnalyzer.Analyze(H2(76, 8, 0, 2, 9m)), "K_tỉ số")); // chủ nhà thua 2 bàn: B1
        Assert.Equal(1.25, Factor(LiveCornerAnalyzer.Analyze(H2(76, 8, 0, 2, 9m, fav: PickSide.Home)), "K_tỉ số"));
        Assert.Equal(1.15, Factor(LiveCornerAnalyzer.Analyze(H2(76, 8, 3, 1, 9m, fav: PickSide.Home)), "K_tỉ số")); // rượt đuổi 4 bàn
        var b2 = LiveCornerAnalyzer.Analyze(H2(76, 8, 2, 0, 9m, fav: PickSide.Home));
        Assert.Equal(0.72, Factor(b2, "K_tỉ số"));
        Assert.True(b2.Settled);
        Assert.Equal(1.35, Factor(LiveCornerAnalyzer.Analyze(H2(76, 8, 1, 0, 9m, fav: PickSide.Away)), "K_tỉ số"));
        Assert.Equal(1.30, Factor(LiveCornerAnalyzer.Analyze(H2(76, 8, 0, 1, 9m)), "K_tỉ số"));
        Assert.Equal(1.20, Factor(LiveCornerAnalyzer.Analyze(H2(76, 8, 1, 0, 9m)), "K_tỉ số"));
        Assert.Equal(0.85, Factor(LiveCornerAnalyzer.Analyze(H2(76, 8, 1, 1, 9m, drawSuits: true)), "K_tỉ số"));
        Assert.Equal(1.18, Factor(LiveCornerAnalyzer.Analyze(H2(76, 8, 0, 0, 9m, handicap: 1.0m)), "K_tỉ số"));
        Assert.Equal(1.25, Factor(LiveCornerAnalyzer.Analyze(H2(76, 8, 0, 0, 9m, handicap: 1.0m, shots: 10, recentShots: 6, recentCorners: 3, recentBlocked: 1)), "K_tỉ số"));
        var slow = LiveCornerAnalyzer.Analyze(H2(76, 8, 0, 0, 9m, shots: 20, recentShots: 1, recentCorners: 0));
        Assert.Equal(0.85, Factor(slow, "K_tỉ số"));
        Assert.Equal(1.00, Factor(LiveCornerAnalyzer.Analyze(H2(76, 8, 0, 0, 9m)), "K_tỉ số"));
    }

    [Fact]
    public void Second_half_bans_follow_score_and_red_cards()
    {
        // 3-0: cấm Tài dù mốc gần.
        var settled = LiveCornerAnalyzer.Analyze(H2(76, 8, 3, 0, 8.5m));
        Assert.True(settled.Edge > 0.4);
        Assert.Equal(CornerCall.NoBet, settled.Call);
        Assert.False(settled.Filters.Single(f => f.Name == "Tỉ số / thẻ đỏ cho phép Tài").Passed);

        // Chủ nhà thua 0-2: cấm Xỉu dù mốc xa.
        var chasing = LiveCornerAnalyzer.Analyze(H2(78, 4, 0, 2, 7.5m));
        Assert.True(chasing.Edge < -0.4);
        Assert.Equal(CornerCall.NoBet, chasing.Call);
        Assert.False(chasing.Filters.Single(f => f.Name == "Tỉ số cho phép Xỉu").Passed);

        // Đội đang dẫn bị thẻ đỏ → 1.25; đội bị dẫn bị thẻ đỏ → 0.72 và cấm Tài.
        Assert.Equal(1.25, Factor(LiveCornerAnalyzer.Analyze(H2(76, 8, 1, 0, 9m, homeRed: 1)), "K_thẻ đỏ"));
        var broken = LiveCornerAnalyzer.Analyze(H2(76, 8, 1, 0, 8.5m, awayRed: 1, fav: PickSide.Away, moti: Motivation.High));
        Assert.Equal(0.72, Factor(broken, "K_thẻ đỏ"));
        if (broken.Edge >= 0)
        {
            Assert.False(broken.Filters.Single(f => f.Name == "Tỉ số / thẻ đỏ cho phép Tài").Passed);
        }
    }

    [Fact]
    public void Second_half_over_only_when_one_and_a_half_to_two_corners_needed()
    {
        // 1-0 cửa trên bị dẫn, cần điểm, 15 phút gần nhất dồn ép: β = 1.35 × 1.12 × 1.15 → kẹp 1.40.
        LiveCornerResult Run(decimal line, decimal over = 1.85m, int minute = 77) =>
            LiveCornerAnalyzer.Analyze(H2(minute, 8, 1, 0, line, h1: 3, fav: PickSide.Away, over: over, moti: Motivation.High,
                shots: 16, recentShots: 7, recentCorners: 3, recentBlocked: 2));

        var ok = Run(10m);
        Assert.Equal(1.40, ok.Multiplier, 6);
        Assert.True(ok.RawMultiplier > 1.40);
        Assert.Equal(2.0, ok.DLine!.Value, 6);
        Assert.Equal(CornerCall.Over, ok.Call);
        Assert.True(ok.Probability!.Push > 0); // mốc nguyên: đúng 2 góc nữa thì hoàn
        Assert.InRange(ok.Confidence, 7.0, 9.5);

        var far = Run(10.5m);
        Assert.Equal(CornerCall.NoBet, far.Call);
        Assert.False(far.Filters.Single(f => f.Name == "Cần thêm 1.5–2 góc").Passed);

        var trap = Run(9m, over: 2.0m, minute: 76);
        Assert.Equal(CornerCall.NoBet, trap.Call);
        Assert.False(trap.Filters.Single(f => f.Name == "Không phải bẫy kèo rẻ").Passed);
    }

    [Fact]
    public void Missing_or_low_odds_is_only_watch()
    {
        var r = LiveCornerAnalyzer.Analyze(H1(28, 4, shots: 9, blocked: 3, da: 30, line: 5.5m, over: 1.60m));
        Assert.Equal(CornerCall.Watch, r.Call);
        Assert.False(r.Filters.Single(f => f.Name == "Odds Tài ≥ 1.78").Passed);
        Assert.Contains("odds", r.Headline);
    }

    [Fact]
    public void Confidence_bands_are_not_inflated()
    {
        Assert.Null(LiveCornerAnalyzer.Confidence(0.61, 1.5));
        Assert.Null(LiveCornerAnalyzer.Confidence(0.80, 0.60));
        Assert.InRange(LiveCornerAnalyzer.Confidence(0.75, 1.20)!.Value, 9.0, 9.5);
        Assert.InRange(LiveCornerAnalyzer.Confidence(0.75, 0.80)!.Value, 8.0, 8.9); // biên chỉ đạt bậc 8
        Assert.InRange(LiveCornerAnalyzer.Confidence(0.63, 1.50)!.Value, 7.0, 7.9);
        Assert.InRange(LiveCornerAnalyzer.Confidence(0.70, 0.62, LiveCornerAnalyzer.MinEdgeH1)!.Value, 7.0, 7.9);
        Assert.True(LiveCornerAnalyzer.Confidence(0.95, 3.0) <= 9.5);
    }

    [Fact]
    public void Best_line_for_over_and_under()
    {
        var over = LiveCornerAnalyzer.BestLine(4, 4.0, PickSide.Over);
        var under = LiveCornerAnalyzer.BestLine(4, 1.0, PickSide.Under);

        Assert.NotNull(over);
        Assert.True(over < 8.5m);
        Assert.NotNull(under);
        Assert.True(under >= 5m);
    }

    [Fact]
    public void Parses_real_live_corner_odds()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "odds-live.json"));
        var envelope = JsonSerializer.Deserialize<ApiEnvelope>(json, ApiFootballClient.Json)!;
        var items = envelope.Response.Deserialize<List<LiveOddsItem>>(ApiFootballClient.Json)!;

        // Trận C2 châu Á 1630370 phút 12: Asian Corners Tài 9 (1.925/1.875), hiệp 1 Tài 3.5 (2.05/1.75).
        var acl2 = LiveOddsParser.Parse(items.Single(i => i.Fixture.Id == 1630370));
        var ft = Assert.Single(acl2, q => q.Window == CornerWindow.SecondHalf);
        Assert.Equal(9m, ft.Line);
        Assert.Equal(1.925m, ft.OverOdds);
        var h1 = Assert.Single(acl2, q => q.Window == CornerWindow.FirstHalf);
        Assert.Equal(3.5m, h1.Line);
        Assert.Equal(1.75m, h1.UnderOdds);
        Assert.Equal(12, h1.Minute);

        var late = LiveOddsParser.Parse(items.Single(i => i.Fixture.Id == 1640328));
        Assert.Equal(9.5m, Assert.Single(late).Line);

        Assert.Empty(LiveOddsParser.Parse(items.Single(i => i.Fixture.Id == 1533790)));
    }

    [Fact]
    public void Parses_live_odds_fetched_per_league()
    {
        // odds/live?league=40 lúc 13:10 UTC thứ Bảy 17/10 (gọi chung odds/live lúc đó chỉ trả về 1 trận của giải khác).
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "odds-live-league.json"));
        var envelope = JsonSerializer.Deserialize<ApiEnvelope>(json, ApiFootballClient.Json)!;
        var items = envelope.Response.Deserialize<List<LiveOddsItem>>(ApiFootballClient.Json)!;

        Assert.Equal([1563207, 1563212, 1563208], items.Select(i => i.Fixture.Id));
        var corners = items.ToDictionary(i => i.Fixture.Id, i => Assert.Single(LiveOddsParser.Parse(i)));
        Assert.All(corners.Values, q => Assert.Equal(CornerWindow.SecondHalf, q.Window));
        Assert.Equal(12.5m, corners[1563207].Line);
        Assert.Equal(6.5m, corners[1563212].Line);
        Assert.Equal(1.95m, corners[1563212].OverOdds);
        Assert.Equal(15m, corners[1563208].Line);
        Assert.Equal(80, corners[1563212].Minute);
        Assert.All(items, i => Assert.NotEmpty(LiveOddsParser.ParseGoals(i)));
    }

    [Fact]
    public void Reads_aiscore_json_into_form()
    {
        // JSON kiểu Claude trả về khi đọc ảnh AiScore (số dạng chuỗi, mốc không hợp lệ bị bỏ).
        var json = SideInfoService.Parse("""
            Đã đọc ảnh:
            ```json
            { "home": "Buriram", "away": "Johor", "window": null, "minute": "27'",
              "home_corners": 3, "away_corners": 1, "home_shots": 6, "away_shots": 3,
              "home_on_target": 2, "away_on_target": 1, "home_blocked": 2, "away_blocked": null,
              "home_dangerous_attacks": 21, "away_dangerous_attacks": 9, "home_possession": "58%",
              "home_goals": 0, "away_goals": 0, "home_red": 0, "away_red": 0,
              "line_open": 4.5, "line_live": "5.5", "over_odds": 1.95, "under_odds": 0.9, "notes": "Buriram dồn cánh trái" }
            ```
            """)!.Value;

        var form = CornerAiService.ParseForm(json);

        Assert.Equal(CornerWindow.FirstHalf, form.Window); // không ghi cửa sổ → suy từ phút 27
        Assert.Equal(27, form.Minute);
        Assert.Equal(9, form.Shots);
        Assert.Equal(3, form.ShotsOnTarget);
        Assert.Equal(2, form.BlockedShots);
        Assert.Equal(30, form.DangerousAttacks);
        Assert.Equal(58, form.HomePossession);
        Assert.Equal(4.5m, form.OpenLine);
        Assert.Equal(5.5m, form.Line);
        Assert.Equal(1.95m, form.OverOdds);
        Assert.Null(form.UnderOdds); // 0.9 không phải odds thập phân
        Assert.Equal("Buriram dồn cánh trái", form.Notes);

        var input = form.ToInput();
        Assert.Equal(4, input.Corners);
        var r = LiveCornerAnalyzer.Analyze(input);
        Assert.True(r.InGoldenWindow);
        Assert.Equal(1.08, r.Factors[1].Value); // 30 TCNH / 27 phút ≥ 1.0
    }

    [Fact]
    public void Second_half_window_from_json_and_commentary_message()
    {
        var form = CornerAiService.ParseForm(SideInfoService.Parse("""{"window":"H2","minute":77,"home_corners":5,"away_corners":4,"home_goals":1,"away_goals":1,"line_live":10.5}""")!.Value);
        Assert.Equal(CornerWindow.SecondHalf, form.Window);
        Assert.Null(form.Shots);

        var input = form.ToInput();
        var message = CornerAiService.UserMessage("A – B", input, LiveCornerAnalyzer.Analyze(input), "ghi chú");
        Assert.Contains("\"phut\": 77", message);
        Assert.Contains("K_tỉ số", message);
        Assert.Contains("kèo góc cả trận", message);
        Assert.Contains("ghi chú", message);
    }

    [Fact]
    public void First_half_corners_flow_from_image_json_to_h2_model_and_claude_data()
    {
        var form = CornerAiService.ParseForm(SideInfoService.Parse("""{"window":"H2","minute":78,"home_corners":6,"away_corners":3,"home_corners_h1":4,"away_corners_h1":3,"home_goals":0,"away_goals":1,"line_live":11,"over_odds":1.9,"under_odds":1.9}""")!.Value);
        Assert.Equal(7, form.FirstHalfCorners);
        form.PreMatchHandicap = -0.75m;

        var input = form.ToInput();
        Assert.Equal(7, input.FirstHalfCorners);
        Assert.Equal(0.75m, input.PreMatchHandicap);
        var r = LiveCornerAnalyzer.Analyze(input);
        Assert.Equal(2.0 / 33, r.SecondHalfRate!.Value, 6);
        Assert.Equal(2.0, r.DLine!.Value, 6);

        var json = JsonDocument.Parse(CornerAiService.DataJson("A – B", input, r, null)).RootElement;
        Assert.Equal(7, json.GetProperty("so_lieu_live").GetProperty("goc").GetProperty("het_hiep_1").GetInt32());
        Assert.Equal(2.0, json.GetProperty("ket_qua_website").GetProperty("d_line").GetDouble());
        Assert.True(json.GetProperty("ket_qua_website").GetProperty("bo_loc_sat").GetArrayLength() > 0);

        // Cửa sổ H1 thì bỏ qua số góc hiệp 1.
        form.Window = CornerWindow.FirstHalf;
        Assert.Null(form.ToInput().FirstHalfCorners);
    }

    [Fact]
    public void Corner_ai_note_snapshots_match_state_and_formula_result()
    {
        var form = CornerAiService.ParseForm(SideInfoService.Parse("""{"home":"A","away":"B","window":"H2","minute":77,"home_corners":5,"away_corners":4,"home_goals":1,"away_goals":0,"line_live":10.5,"over_odds":1.9,"under_odds":1.9}""")!.Value);
        Assert.Equal("A – B", CornerAiService.FormMatch(form));
        Assert.Equal("Trận nhập tay", CornerAiService.FormMatch(new LiveCornerForm()));

        var input = form.ToInput();
        var result = LiveCornerAnalyzer.Analyze(input);
        var note = CornerAiService.NoteFor(CornerAiKind.Commentary, new string('x', 250), 42, input, result);
        Assert.Equal(200, note.Match.Length);
        Assert.Equal(42, note.FixtureId);
        Assert.Equal(CornerWindow.SecondHalf, note.Window);
        Assert.Equal(77, note.Minute);
        Assert.Equal(9, note.TotalCorners);
        Assert.Equal((1, 0), (note.HomeGoals, note.AwayGoals));
        Assert.Equal(10.5m, note.Line);
        Assert.Equal(Math.Round(result.ExpectedTotal, 2), note.ExpectedTotal);
        Assert.Equal(result.Headline, note.Headline);
        Assert.True(note.Complete);

        // Dữ liệu gửi Claude lưu kèm là JSON hợp lệ (chính phần nằm trong tin nhắn).
        var json = CornerAiService.DataJson("A – B", input, result, null);
        Assert.Equal(77, JsonDocument.Parse(json).RootElement.GetProperty("so_lieu_live").GetProperty("phut").GetInt32());
        Assert.Contains(json, CornerAiService.UserMessage("A – B", input, result, null));

        // Đọc ảnh chưa có phút: không có kết luận công thức.
        Assert.Null(CornerAiService.NoteFor(CornerAiKind.ReadImages, "A – B", null, new LiveCornerForm().ToInput(), null).Headline);
    }
}
