using Prediction.Core.Engine;
using Prediction.Core.Engine.Analysis;
using Prediction.Core.Entities;

namespace Prediction.Tests;

public class LinePatternTests
{
    private static readonly DateTime Kickoff = new(2026, 10, 17, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Kickoff.AddHours(-5);

    private static List<TeamMatch> History(int n, int gf, int ga) =>
        Enumerable.Range(0, n).Select(i => new TeamMatch(1000 + i, Kickoff.AddDays(-7 - 7 * i), i % 2 == 0, 99, gf, ga, 5, 5, 2, 2)).ToList();

    private static MarketMovement Line(Market market, decimal line, decimal a = 1.90m, decimal b = 1.90m, decimal? open = null)
    {
        var points = new List<OddsPoint>();
        if (open is { } o)
        {
            points.Add(new OddsPoint(Kickoff.AddDays(-2), o, a, b, OddsSource.Api));
        }

        points.Add(new OddsPoint(Kickoff.AddHours(-6), line, a, b, OddsSource.Api));
        return new MarketMovement { Market = market, Points = points };
    }

    private static AnalysisInput Input(decimal? ah = null, decimal? ou = null, decimal? ahOpen = null, decimal? ouOpen = null,
        IReadOnlyList<TeamMatch>? home = null, IReadOnlyList<TeamMatch>? away = null, DnaTag homeTags = DnaTag.None, DnaTag awayTags = DnaTag.None) => new()
        {
            Fixture = new Fixture { Id = 700, LeagueId = 78, Season = 2026, Round = "Regular Season - 8", KickoffUtc = Kickoff, HomeTeamId = 1, AwayTeamId = 2 },
            HomeName = "Nhà",
            AwayName = "Khách",
            LeagueName = "Bundesliga",
            HomeDna = homeTags == DnaTag.None ? null : new TeamDna { TeamId = 1, TeamName = "Nhà", Tags = homeTags },
            AwayDna = awayTags == DnaTag.None ? null : new TeamDna { TeamId = 2, TeamName = "Khách", Tags = awayTags },
            TableSize = 18,
            HomeHistory = home ?? History(10, 2, 1),
            AwayHistory = away ?? History(10, 1, 2),
            League = new LeagueBaseline(300, 2.9, 300, 10.0, 4.2),
            Handicap = ah is { } h ? Line(Market.AsianHandicap, h, open: ahOpen) : null,
            Totals = ou is { } t ? Line(Market.OverUnder, t, open: ouOpen) : null,
        };

    private static LinePatternResult Score(AnalysisInput input, LinePattern pattern, bool lineups = false) =>
        LinePatternScorer.Score(input, MatchAnalyzer.Analyze(input), pattern, Now, lineups);

    [Fact]
    public void Detects_only_exact_lines()
    {
        Assert.Equal([LinePattern.Over3, LinePattern.Favourite075], LinePatternScorer.Detect(Input(ah: -0.75m, ou: 3.0m)));
        Assert.Equal([LinePattern.Favourite1], LinePatternScorer.Detect(Input(ah: 1.0m, ou: 2.75m)));
        Assert.Empty(LinePatternScorer.Detect(Input(ah: -0.5m, ou: 3.25m)));
        Assert.Empty(LinePatternScorer.Detect(Input(ah: -1.25m, ou: 2.5m)));
    }

    [Fact]
    public void Over3_pushes_on_exactly_three_goals()
    {
        var r = Score(Input(ou: 3.0m), LinePattern.Over3);
        Assert.Equal(PickSide.Over, r.Side);
        Assert.Equal(3.0m, r.Line);
        Assert.True(r.ModelProbability.Push > 0.1);
        Assert.Equal(0, r.ModelProbability.HalfWin);
        Assert.Equal(0, r.ModelProbability.HalfLoss);
        Assert.Equal(1.0, r.ModelProbability.Win + r.ModelProbability.Push + r.ModelProbability.Loss, 3);
    }

    [Fact]
    public void Favourite_side_and_line_follow_the_handicap()
    {
        var home = Score(Input(ah: -1.0m), LinePattern.Favourite1);
        Assert.Equal(PickSide.Home, home.Side);
        Assert.Equal(-1.0m, home.Line);
        Assert.True(home.ModelProbability.Push > 0.1); // thắng cách 1 bàn: hoàn tiền

        var away = Score(Input(ah: 0.75m, home: History(10, 1, 2), away: History(10, 2, 1)), LinePattern.Favourite075);
        Assert.Equal(PickSide.Away, away.Side);
        Assert.Equal(-0.75m, away.Line);
        Assert.True(away.ModelProbability.HalfWin > 0.1); // thắng cách 1 bàn: ăn nửa
        Assert.Equal(0, away.ModelProbability.Push);
        Assert.Contains(away.Indicators, i => i.Name == "Sân" && i.Points < 0);
        Assert.Contains(home.Indicators, i => i.Name == "Sân" && i.Points > 0);
    }

    [Fact]
    public void Min_odds_is_break_even()
    {
        var r = Score(Input(ou: 3.0m), LinePattern.Over3);
        Assert.NotNull(r.MinOdds);
        Assert.Equal(0, r.Probability.ExpectedValue(r.MinOdds!.Value), 2);
    }

    [Fact]
    public void Line_moving_toward_the_side_scores_higher_than_against()
    {
        var goals = History(10, 3, 2);
        var toward = Score(Input(ou: 3.0m, ouOpen: 2.75m, home: goals, away: goals), LinePattern.Over3);
        var against = Score(Input(ou: 3.0m, ouOpen: 3.25m, home: goals, away: goals), LinePattern.Over3);
        Assert.Equal(Lean.Toward, toward.Lean);
        Assert.Equal(Lean.Against, against.Lean);
        Assert.True(toward.Score > against.Score);
        Assert.Contains(against.Indicators, i => i.Name == "Biến động kèo" && i.Points < 0);
    }

    [Fact]
    public void Low_sample_caps_below_enter_score()
    {
        var r = Score(Input(ou: 3.0m, home: History(3, 4, 2), away: History(3, 3, 3)), LinePattern.Over3);
        Assert.True(r.Score <= 7.9);
        Assert.Contains(r.Indicators, i => i.Name == "Mẫu dữ liệu" && i.Points == -1.0);
    }

    [Fact]
    public void Negative_ev_never_reaches_consider()
    {
        // Mô hình chỉ ~1 bàn/trận mà kèo Tài 3.0: EV âm → tối đa 5.9, không vào.
        var r = Score(Input(ou: 3.0m, home: History(10, 0, 1), away: History(10, 1, 0)), LinePattern.Over3);
        Assert.True(r.ExpectedValue < 0);
        Assert.True(r.Score <= 5.9);
        Assert.Equal(EntryTiming.Skip, r.Entry.Timing);
    }

    [Fact]
    public void Entry_advice_follows_score_lean_and_lineups()
    {
        var ko = Kickoff;
        Assert.Equal(EntryTiming.Skip, LinePatternScorer.Entry(LinePattern.Over3, 5.5, Lean.Neutral, false, ko, Now, 3m, 1.9m).Timing);
        Assert.Equal(EntryTiming.Now, LinePatternScorer.Entry(LinePattern.Over3, 8.4, Lean.Toward, false, ko, Now, 3m, 1.9m).Timing);
        Assert.Equal(EntryTiming.Now, LinePatternScorer.Entry(LinePattern.Over3, 6.5, Lean.Neutral, true, ko, Now, 3m, 1.9m).Timing);

        var wait = LinePatternScorer.Entry(LinePattern.Favourite1, 8.4, Lean.Against, false, ko, Now, -1m, 1.9m);
        Assert.Equal(EntryTiming.WaitLineups, wait.Timing);
        Assert.Contains("chấp 1.0 xuống 0.75", wait.Text);
        Assert.Equal(ko.AddMinutes(-60), wait.FromUtc);

        var window = LinePatternScorer.Entry(LinePattern.Over3, 7.0, Lean.Neutral, false, ko, Now, 3m, 1.9m);
        Assert.Equal(EntryTiming.Window, window.Timing);
        Assert.Equal(ko.AddMinutes(-60), window.FromUtc);
        Assert.Equal(ko.AddMinutes(-30), window.ToUtc);
        Assert.Contains("giá còn ≥ 1.90", window.Text);

        Assert.Equal(EntryTiming.Skip, LinePatternScorer.Entry(LinePattern.Over3, 9.0, Lean.Toward, true, ko, ko.AddMinutes(-5), 3m, 1.9m).Timing);

        var late = LinePatternScorer.Entry(LinePattern.Over3, 7.0, Lean.Neutral, false, ko, ko.AddMinutes(-20), 3m, 1.9m);
        Assert.Equal(EntryTiming.Now, late.Timing);
        Assert.Equal(ko.AddMinutes(-20), late.FromUtc);
        Assert.Equal(ko.AddMinutes(-10), late.ToUtc);
    }

    [Fact]
    public void Red_flag_on_the_side_caps_score()
    {
        // Cờ 1: đội "yếu sân khách" làm khách mà phải chấp 0.75 → chặn cửa trên dù số liệu đẹp.
        var input = Input(ah: 0.75m, home: History(10, 0, 2), away: History(10, 3, 0), awayTags: DnaTag.AwayFragile);
        var r = Score(input, LinePattern.Favourite075);
        Assert.Equal(PickSide.Away, r.Side);
        Assert.Contains(r.Indicators, i => i.Name == "Cờ đỏ" && i.Points < 0);
        Assert.True(r.Score <= 5.9);
        Assert.Contains(r.Indicators, i => i.Name == "DNA" && i.Points < 0);
    }
}
