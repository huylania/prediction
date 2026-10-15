using System.Text.Json;
using Prediction.Core.Engine.Analysis;
using Prediction.Core.Engine.LiveCorners;
using Prediction.Core.Entities;
using Prediction.Web.ApiFootball;
using Prediction.Web.Services;

namespace Prediction.Tests;

public class LiveCornerTests
{
    [Fact]
    public void First_half_over_with_heavy_pressure()
    {
        // Phút 28, 4 góc, 9 cú sút (3 bị chặn), 30 tấn công nguy hiểm, mốc H1 6.5.
        var r = LiveCornerAnalyzer.Analyze(new LiveCornerInput
        {
            Window = CornerWindow.FirstHalf, Minute = 28, HomeCorners = 3, AwayCorners = 1,
            Shots = 9, BlockedShots = 3, DangerousAttacks = 30, Line = 6.5m, OverOdds = 1.85m, UnderOdds = 1.95m,
        });

        Assert.Equal(4.0 / 28, r.Rate, 6);
        Assert.Equal(1.25, r.Factors[0].Value);
        Assert.Equal(1.20, r.Factors[1].Value);
        Assert.Equal(1.5, r.Multiplier, 6);
        Assert.Equal(19.5, r.RemainingMinutes, 6);
        Assert.Equal(4 + 4.0 / 28 * 1.5 * 19.5, r.ExpectedTotal, 6);
        Assert.Equal(CornerCall.Over, r.Call);
        Assert.True(r.InGoldenWindow);
        Assert.True(r.Probability!.WinAny > 0.6);
        Assert.True(r.Confidence >= 7);
        Assert.Contains("6.5", r.Headline);
    }

    [Fact]
    public void First_half_under_when_slow_and_proxy_tempo_used()
    {
        var r = LiveCornerAnalyzer.Analyze(new LiveCornerInput
        {
            Window = CornerWindow.FirstHalf, Minute = 27, HomeCorners = 1, AwayCorners = 0,
            Shots = 2, BlockedShots = 0, Line = 3.5m, OverOdds = 1.9m, UnderOdds = 1.9m,
        });

        Assert.Equal(0.80, r.Factors[0].Value);
        Assert.Equal(0.80, r.Factors[1].Value);
        Assert.Contains("chỉ số thay thế", r.Factors[1].Reason);
        Assert.Equal(CornerCall.Under, r.Call);
        Assert.Equal(PickSide.Under, r.Side);
    }

    [Fact]
    public void Thin_edge_is_no_bet_and_medium_edge_is_watch()
    {
        LiveCornerResult Run(decimal line) => LiveCornerAnalyzer.Analyze(new LiveCornerInput
        {
            Window = CornerWindow.FirstHalf, Minute = 28, HomeCorners = 2, AwayCorners = 2, Shots = 5, BlockedShots = 1, Line = line,
        });

        var exp = Run(5m).ExpectedTotal; // α = 1 (4–5 cú sút; (5+4)/28 = 0.32 → nhịp trung bình)
        Assert.Equal(4 + 4.0 / 28 * 19.5, exp, 6);
        Assert.Equal(CornerCall.NoBet, Run(Math.Round((decimal)exp * 4) / 4).Call);
        Assert.Equal(CornerCall.Watch, Run(6.25m).Call); // biên ≈ +0.54
    }

    [Fact]
    public void Second_half_over_when_game_open_and_pressure_rising()
    {
        // Phút 77, 1-0 cho đội nhà nhưng cửa trên là đội khách; 15 phút gần nhất dồn ép.
        var r = LiveCornerAnalyzer.Analyze(new LiveCornerInput
        {
            Window = CornerWindow.SecondHalf, Minute = 77, HomeCorners = 3, AwayCorners = 5, HomeGoals = 1, AwayGoals = 0,
            Shots = 20, BlockedShots = 5, RecentMinutes = 15, RecentShots = 8, RecentCorners = 3, RecentBlocked = 2,
            Motivation = Motivation.High, Favourite = PickSide.Away, Line = 10.5m, OverOdds = 1.9m, UnderOdds = 1.9m,
        });

        Assert.Equal(1.40, r.Factors[0].Value); // cửa trên bị dẫn
        Assert.Equal(1.20, r.Factors[1].Value);
        Assert.Equal(1.30, r.Factors[2].Value);
        Assert.Equal(1.00, r.Factors[3].Value);
        Assert.Equal(1.4 * 1.2 * 1.3, r.Multiplier, 6);
        Assert.Equal(18.5, r.RemainingMinutes, 6);
        Assert.False(r.Settled);
        Assert.Equal(CornerCall.Over, r.Call);
    }

    [Fact]
    public void Second_half_under_when_settled_and_trailing_side_down_to_ten()
    {
        var r = LiveCornerAnalyzer.Analyze(new LiveCornerInput
        {
            Window = CornerWindow.SecondHalf, Minute = 76, HomeCorners = 6, AwayCorners = 3, HomeGoals = 3, AwayGoals = 0, AwayRed = 1,
            Shots = 22, RecentMinutes = 15, RecentShots = 2, RecentCorners = 0,
            Motivation = Motivation.Low, Line = 11m, OverOdds = 1.9m, UnderOdds = 1.85m,
        });

        Assert.Equal(0.65, r.Factors[0].Value);
        Assert.Equal(0.85, r.Factors[1].Value);
        Assert.Equal(0.75, r.Factors[2].Value);
        Assert.Equal(0.75, r.Factors[3].Value); // đội bị dẫn thiếu người
        Assert.True(r.Settled);
        Assert.Equal(CornerCall.Under, r.Call);
        // Mốc nguyên 11: đúng 11 góc thì hoàn tiền.
        Assert.True(r.Probability!.Push > 0);
    }

    [Fact]
    public void Leader_down_to_ten_and_settled_draw()
    {
        var red = LiveCornerAnalyzer.Analyze(new LiveCornerInput
        {
            Window = CornerWindow.SecondHalf, Minute = 75, HomeCorners = 4, AwayCorners = 4, HomeGoals = 1, AwayGoals = 0, HomeRed = 1, Line = 10m,
        });
        Assert.Equal(1.30, red.Factors[3].Value);

        var draw = LiveCornerAnalyzer.Analyze(new LiveCornerInput
        {
            Window = CornerWindow.SecondHalf, Minute = 78, HomeCorners = 5, AwayCorners = 5, HomeGoals = 1, AwayGoals = 1, DrawSuitsBoth = true, Line = 13m,
        });
        Assert.True(draw.Settled);
        Assert.Equal(0.75, draw.Factors[0].Value);
    }

    [Fact]
    public void Open_game_with_negative_edge_is_only_watch()
    {
        var r = LiveCornerAnalyzer.Analyze(new LiveCornerInput
        {
            Window = CornerWindow.SecondHalf, Minute = 78, HomeCorners = 2, AwayCorners = 2, HomeGoals = 0, AwayGoals = 0, Line = 7.5m,
        });

        Assert.Equal(CornerCall.Watch, r.Call);
        Assert.Contains(r.Notes, n => n.Contains("an bài"));
    }

    [Fact]
    public void Low_odds_turn_signal_into_no_bet()
    {
        var r = LiveCornerAnalyzer.Analyze(new LiveCornerInput
        {
            Window = CornerWindow.FirstHalf, Minute = 28, HomeCorners = 3, AwayCorners = 1,
            Shots = 9, BlockedShots = 3, DangerousAttacks = 30, Line = 6.5m, OverOdds = 1.08m, UnderOdds = 7m,
        });

        Assert.Equal(CornerCall.NoBet, r.Call);
        Assert.Contains(r.Notes, n => n.Contains("EV âm"));
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
        Assert.Equal(1.20, r.Factors[1].Value); // 30 TCNH / 27 phút ≥ 1.0
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
}
