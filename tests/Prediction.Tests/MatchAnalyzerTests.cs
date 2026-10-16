using Prediction.Core.Engine;
using Prediction.Core.Engine.Analysis;
using Prediction.Core.Entities;

namespace Prediction.Tests;

public class MatchAnalyzerTests
{
    private static readonly DateTime Kickoff = new(2026, 10, 17, 14, 0, 0, DateTimeKind.Utc);

    private const int HomeId = 1;
    private const int AwayId = 2;

    /// <summary>n trận cách nhau 7 ngày trước trận cần phân tích, mới nhất trước, xen kẽ sân nhà/sân khách.</summary>
    private static List<TeamMatch> History(int n, int gf, int ga, int? cf = null, int? ca = null, int startDaysBefore = 7) =>
        Enumerable.Range(0, n)
            .Select(i => new TeamMatch(1000 + i, Kickoff.AddDays(-startDaysBefore - 7 * i), i % 2 == 0, 99, gf, ga, cf, ca, 2, 2))
            .ToList();

    private static List<TeamMatch> Mixed(params (int Gf, int Ga)[] results) =>
        results.Select((r, i) => new TeamMatch(2000 + i, Kickoff.AddDays(-7 - 7 * i), i % 2 == 0, 99, r.Gf, r.Ga)).ToList();

    private static Fixture Fixture(string? round = "Regular Season - 8") => new()
    {
        Id = 500,
        LeagueId = 78,
        Season = 2026,
        Round = round,
        KickoffUtc = Kickoff,
        HomeTeamId = HomeId,
        AwayTeamId = AwayId,
    };

    private static TeamSnapshot Standing(int id, int rank, int goalDiff, int played = 10) => new()
    {
        TeamId = id,
        Rank = rank,
        Played = played,
        GoalsFor = 15 + Math.Max(goalDiff, 0),
        GoalsAgainst = 15 + Math.Max(-goalDiff, 0),
    };

    private static MarketMovement Line(Market market, decimal line, decimal a = 1.90m, decimal b = 1.90m, decimal? open = null)
    {
        var points = new List<OddsPoint>();
        if (open is { } o)
        {
            points.Add(new OddsPoint(Kickoff.AddDays(-3), o, a, b, OddsSource.Api));
        }

        points.Add(new OddsPoint(Kickoff.AddHours(-1), line, a, b, OddsSource.Api));
        return new MarketMovement { Market = market, Points = points };
    }

    private static AnalysisInput Input(
        IReadOnlyList<TeamMatch>? home = null,
        IReadOnlyList<TeamMatch>? away = null,
        decimal? ah = null,
        decimal? ou = null,
        DnaTag league = DnaTag.None,
        DnaTag homeTags = DnaTag.None,
        DnaTag awayTags = DnaTag.None,
        TeamSnapshot? homeStanding = null,
        TeamSnapshot? awayStanding = null,
        decimal? ahOpen = null,
        decimal? ouOpen = null,
        decimal ouOver = 1.90m,
        decimal ouUnder = 1.90m,
        decimal? corners = null,
        decimal? cornersHandicap = null,
        string? round = "Regular Season - 8",
        IReadOnlyList<TeamChange>? homeChanges = null,
        IReadOnlyList<FixtureInjury>? injuries = null,
        string? leagueType = null) => new()
        {
            Fixture = Fixture(round),
            LeagueType = leagueType,
            HomeName = "Nhà",
            AwayName = "Khách",
            LeagueName = "Bundesliga",
            LeagueDna = league == DnaTag.None ? null : new LeagueDna { LeagueId = 78, LeagueName = "Bundesliga", Tags = league },
            HomeDna = homeTags == DnaTag.None ? null : new TeamDna { TeamId = HomeId, TeamName = "Nhà", Tags = homeTags },
            AwayDna = awayTags == DnaTag.None ? null : new TeamDna { TeamId = AwayId, TeamName = "Khách", Tags = awayTags },
            HomeStanding = homeStanding,
            AwayStanding = awayStanding,
            TableSize = 18,
            HomeHistory = home ?? History(10, 2, 1),
            AwayHistory = away ?? History(10, 1, 2),
            League = new LeagueBaseline(300, 2.7, 300, 10.0, 4.2),
            Handicap = ah is { } h ? Line(Market.AsianHandicap, h, open: ahOpen) : null,
            Totals = ou is { } t ? Line(Market.OverUnder, t, ouOver, ouUnder, ouOpen) : null,
            Corners = corners is { } c ? Line(Market.CornersOverUnder, c) : null,
            CornersHandicap = cornersHandicap is { } ch ? Line(Market.CornersHandicap, ch) : null,
            HomeChanges = homeChanges ?? [],
            Injuries = injuries ?? [],
        };

    // ------------------------------------------------------------ Phân phối & chấm kèo

    [Fact]
    public void Poisson_totals_match_closed_form()
    {
        var total = Distributions.Sum(Distributions.Poisson(1.35, 10), Distributions.Poisson(1.35, 10));
        var over25 = Distributions.Totals(total, PickSide.Over, 2.5m);
        var over225 = Distributions.Totals(total, PickSide.Over, 2.25m);

        // P(tổng ≥ 3) với Poisson(2.7) = 1 − e^-2.7 (1 + 2.7 + 2.7²/2) ≈ 0.5064
        Assert.Equal(0.5064, over25.Win, 3);
        Assert.Equal(0, over25.Push);
        // Tài 2.25: tổng đúng 2 bàn thua nửa tiền.
        Assert.Equal(0.2450, over225.HalfLoss, 3);
        Assert.Equal(over25.Win, over225.Win, 6);
    }

    [Fact]
    public void Level_ball_handicap_pushes_on_draw()
    {
        var diff = Distributions.Difference(Distributions.Poisson(1.3, 10), Distributions.Poisson(1.3, 10));
        var home = Distributions.Handicap(diff, PickSide.Home, 0m);
        var away = Distributions.Handicap(diff, PickSide.Away, 0m);

        Assert.Equal(home.Win, away.Win, 6);
        Assert.Equal(diff.Diff[diff.Offset], home.Push, 6);
        Assert.Equal(1.0, home.Win + home.Push + home.Loss, 6);
    }

    [Fact]
    public void Expected_value_uses_odds_and_half_results()
    {
        Assert.Equal(0, new PickProbability(0.5, 0, 0, 0, 0.5).ExpectedValue(2.0m), 6);
        // Thắng nửa ở odds 1.90 = +0.45, thua nửa = −0.5.
        Assert.Equal(0.45 * 0.5 - 0.5 * 0.5, new PickProbability(0, 0.5, 0, 0.5, 0).ExpectedValue(1.90m), 6);
    }

    [Fact]
    public void Negative_binomial_keeps_mean_and_adds_variance()
    {
        var p = Distributions.NegativeBinomial(10, 1.4, 40);
        var mean = p.Select((x, k) => x * k).Sum();
        var variance = p.Select((x, k) => x * (k - mean) * (k - mean)).Sum();

        Assert.Equal(10, mean, 1);
        Assert.Equal(14, variance, 0);
    }

    [Theory]
    [InlineData(1.3, -1.25)]
    [InlineData(0.125, -0.25)]
    [InlineData(-0.375, 0.5)]
    [InlineData(0.1, 0)]
    public void Handicap_rounds_to_quarter(double supremacy, double expected) =>
        Assert.Equal((decimal)expected, -MatchAnalyzer.RoundQuarter(supremacy));

    // ------------------------------------------------------------ Nhân sự

    [Fact]
    public void New_manager_buffer_and_stabilised_sample()
    {
        var history = History(10, 2, 1);
        var change = new TeamChange { TeamId = HomeId, Kind = TeamChangeKind.NewManager, PersonName = "X", StartDate = Kickoff.AddDays(-20) };

        var buffer = TeamFormBuilder.Regime([change], history, Kickoff);
        Assert.NotNull(buffer);
        Assert.Equal(2, buffer.MatchesUnder); // trận -7 và -14 ngày
        Assert.True(buffer.InBuffer);
        Assert.Equal(3, buffer.UpcomingMatchNumber);

        change.StartDate = Kickoff.AddDays(-25);
        var stable = TeamFormBuilder.Regime([change], history, Kickoff)!;
        Assert.False(stable.InBuffer);
        Assert.True(stable.UsesNewSample);

        var form = TeamFormBuilder.Build(history, true, stable);
        Assert.Equal(3, form.Matches);
        Assert.Contains("HLV X", form.SampleLabel);

        change.IsIgnored = true;
        Assert.Null(TeamFormBuilder.Regime([change], history, Kickoff));
    }

    // ------------------------------------------------------------ H_exp & bàn thắng

    [Fact]
    public void Model_handicap_and_goal_expectation_follow_playbook_formulas()
    {
        var a = MatchAnalyzer.Analyze(Input(ah: -1.25m, ou: 2.5m,
            homeStanding: Standing(HomeId, 2, 10), awayStanding: Standing(AwayId, 15, -10)));

        // λ nhà = (2 + 2) / 2, λ khách = (1 + 1) / 2 → phần phong độ 1.0; phần BXH (1 − (−1)) / 2 = 1.0.
        Assert.Equal(1.0, a.Handicap.FormPart, 6);
        Assert.Equal(1.0, a.Handicap.TablePart!.Value, 6);
        Assert.Equal(0.30, a.Handicap.HomeAdvantage, 6);
        Assert.Equal(1.3, a.Handicap.Supremacy, 6);
        Assert.Equal(-1.25m, a.Handicap.HExp);
        Assert.Equal(0m, a.Handicap.Delta);
        Assert.False(a.Handicap.IsAnomaly);

        Assert.Equal(3.0, a.Goals.Expected!.Value, 6);
        Assert.Equal(0.5, a.Goals.Delta!.Value, 6);
        Assert.Equal(PickSide.Over, a.Goals.Lean);
        var over = a.Picks.Single(p => p.Market == Market.OverUnder);
        Assert.Equal(PickSide.Over, over.Side);
        Assert.True(over.MeetsRule);
        Assert.Equal(1.0, a.HomeWinProbability + a.DrawProbability + a.AwayWinProbability, 6);
        Assert.True(a.HomeWinProbability > a.AwayWinProbability);
    }

    [Fact]
    public void Dna_adjusts_home_advantage()
    {
        var a = MatchAnalyzer.Analyze(Input(ah: -1m, league: DnaTag.HomeFortress, homeTags: DnaTag.HomeFortress, awayTags: DnaTag.AwayFragile));

        // 0.60 + 0.25 + 0.25 nhưng tổng lợi thế sân theo DNA bị chặn ở 0.75 (khung của file kỹ năng).
        Assert.Equal(MatchAnalyzer.MaxDnaHomeAdvantage, a.Handicap.HomeAdvantage, 6);
        Assert.Equal(4, a.Handicap.HomeAdvantageNotes.Count);
        Assert.Contains(a.Handicap.HomeAdvantageNotes, n => n.Contains("tối đa +0.75"));
    }

    [Fact]
    public void High_altitude_venue_adds_home_advantage()
    {
        var input = Input(ah: -1m);
        var high = MatchAnalyzer.Analyze(new AnalysisInput
        {
            Fixture = input.Fixture,
            HomeHistory = input.HomeHistory,
            AwayHistory = input.AwayHistory,
            League = input.League,
            Handicap = input.Handicap,
            VenueElevationM = 3640,
        });

        Assert.Equal(0.30 + 0.25, high.Handicap.HomeAdvantage, 6);
        Assert.Contains(high.Handicap.HomeAdvantageNotes, n => n.Contains("3640 m"));
    }

    [Fact]
    public void Under_requires_expectation_below_league_average()
    {
        var a = MatchAnalyzer.Analyze(Input(home: History(10, 1, 0), away: History(10, 0, 1), ou: 2.5m));

        // Kỳ vọng (1 + 1 + 0 + 0) / 2 = 1.0, Δ = −1.5 → Xỉu.
        Assert.Equal(1.0, a.Goals.Expected!.Value, 6);
        Assert.Equal(PickSide.Under, a.Goals.Lean);
        Assert.Equal(PickSide.Under, a.Picks.Single(p => p.Market == Market.OverUnder).Side);
    }

    // ------------------------------------------------------------ Kèo lạ

    [Fact]
    public void Unexplained_anomaly_is_a_suspected_trap()
    {
        var a = MatchAnalyzer.Analyze(Input(ah: -0.25m, ou: 2.5m,
            homeStanding: Standing(HomeId, 2, 10), awayStanding: Standing(AwayId, 15, -10)));

        Assert.Equal(1.0m, a.Handicap.Delta);
        Assert.True(a.Handicap.IsAnomaly);
        Assert.Equal(HandicapVerdict.SuspectedTrap, a.Handicap.Verdict);
        Assert.Contains(a.Flags, f => f.Number == 2);
        Assert.All(a.Picks.Where(p => p.Market is Market.AsianHandicap or Market.OverUnder), p => Assert.Equal(AnalysisGroup.C, p.Group));
    }

    [Fact]
    public void Anomaly_explained_by_absences_is_real_strength()
    {
        var injuries = Enumerable.Range(0, 4)
            .Select(i => new FixtureInjury { FixtureId = 500, TeamId = HomeId, PlayerName = $"P{i}", Type = "Missing Fixture" })
            .ToList();
        var a = MatchAnalyzer.Analyze(Input(ah: -0.25m, ou: 2.5m, injuries: injuries,
            homeStanding: Standing(HomeId, 2, 10), awayStanding: Standing(AwayId, 15, -10)));

        Assert.Equal(HandicapVerdict.RealStrength, a.Handicap.Verdict);
        Assert.Contains(a.Handicap.Causes, c => c.Contains("vắng 4"));
        Assert.DoesNotContain(a.Flags, f => f.Number == 2);
        var ah = a.Picks.Single(p => p.Market == Market.AsianHandicap);
        Assert.Equal(PickSide.Away, ah.Side); // thị trường đã tính việc đội nhà thiếu người
        Assert.NotEqual(AnalysisGroup.A, ah.Group);
        Assert.Equal(4, a.HomeAbsent.Count);
    }

    [Fact]
    public void Close_ranks_with_deep_line_is_scenario_a()
    {
        var a = MatchAnalyzer.Analyze(Input(home: History(10, 1, 1), away: History(10, 1, 1), ah: -1m,
            homeStanding: Standing(HomeId, 6, 2), awayStanding: Standing(AwayId, 8, 0)));

        Assert.Equal(HandicapScenario.DeepLineCloseRanks, a.Handicap.Scenario);
        Assert.Equal(HandicapVerdict.SuspectedTrap, a.Handicap.Verdict);
    }

    [Fact]
    public void Better_home_side_on_level_ball_is_scenario_b()
    {
        var a = MatchAnalyzer.Analyze(Input(home: History(10, 1, 1), away: History(10, 1, 1), ah: 0m,
            homeStanding: Standing(HomeId, 3, 8), awayStanding: Standing(AwayId, 14, -6)));

        Assert.Equal(HandicapScenario.ShallowLineForBetterHome, a.Handicap.Scenario);
        Assert.Contains("THƠM ẢO", a.Handicap.Summary);
        var flag = Assert.Single(a.Flags, f => f.Number == 2);
        Assert.True(flag.Covers(Market.AsianHandicap, PickSide.Home));
    }

    // ------------------------------------------------------------ Cờ đỏ

    [Fact]
    public void Red_flag_1_away_fragile_favourite()
    {
        var a = MatchAnalyzer.Analyze(Input(ah: 0.5m, ou: 2.5m, awayTags: DnaTag.HomeFortress | DnaTag.AwayFragile));

        var flag = Assert.Single(a.Flags, f => f.Number == 1);
        Assert.True(flag.Covers(Market.AsianHandicap, PickSide.Away));
        Assert.True(flag.Covers(Market.OverUnder, PickSide.Under));
        Assert.False(flag.Covers(Market.AsianHandicap, PickSide.Home));
    }

    [Fact]
    public void Red_flag_2_line_jump_against_form()
    {
        // Kèo mở đúng dự phóng (−1.25) rồi dịch 0.5 về phía đội khách; mốc tài xỉu giảm 0.5 dù kỳ vọng 3.0 bàn.
        var a = MatchAnalyzer.Analyze(Input(ah: -0.75m, ahOpen: -1.25m, ou: 2.25m, ouOpen: 2.75m));

        var flag = Assert.Single(a.Flags, f => f.Number == 2);
        Assert.Contains("nhảy", flag.Detail);
        Assert.Contains("mốc tài xỉu giảm", flag.Detail);
    }

    [Fact]
    public void Red_flag_3_dead_lock()
    {
        var home = Mixed((0, 0), (0, 0), (1, 1), (0, 0), (1, 0), (0, 1), (0, 0), (1, 0), (0, 1), (0, 0));
        var away = Mixed((0, 0), (1, 1), (0, 0), (0, 0), (0, 1), (1, 0), (0, 0), (0, 2), (1, 0), (0, 0));
        var a = MatchAnalyzer.Analyze(Input(home: home, away: away, ah: 0m));

        var flag = Assert.Single(a.Flags, f => f.Number == 3);
        Assert.True(flag.Covers(Market.AsianHandicap, PickSide.Home));
        Assert.Equal(AnalysisGroup.C, a.Picks.Single(p => p.Market == Market.AsianHandicap).Group);
    }

    [Theory]
    [InlineData("League Stage - 8", true)]
    [InlineData("League Stage - 3", false)]
    [InlineData("League Phase - 7", true)]
    [InlineData("2nd Qualifying Round", true)]
    [InlineData("Regular Season - 34", false)]
    public void Red_flag_4_motivation(string round, bool expected)
    {
        var a = MatchAnalyzer.Analyze(Input(ah: -1.25m, round: round));

        Assert.Equal(expected, a.Flags.Any(f => f.Number == 4));
    }

    [Fact]
    public void Red_flag_5_new_manager_buffer_discards_everything()
    {
        var change = new TeamChange { TeamId = HomeId, Kind = TeamChangeKind.NewManager, PersonName = "HLV Mới", StartDate = Kickoff.AddDays(-10) };
        var a = MatchAnalyzer.Analyze(Input(ah: -1.25m, ou: 2.5m, homeChanges: [change]));

        Assert.True(a.HomeRegime!.InBuffer);
        Assert.Equal(2, a.HomeRegime.UpcomingMatchNumber);
        Assert.Contains(a.Flags, f => f.Number == 5);
        Assert.All(a.Picks, p => Assert.Equal(AnalysisGroup.C, p.Group));
        Assert.Contains(a.InPlayHints, h => h.Contains("20–25'"));
    }

    // ------------------------------------------------------------ Chấm điểm & nhóm

    [Fact]
    public void Strong_confluence_reaches_group_a()
    {
        var a = MatchAnalyzer.Analyze(Input(home: History(10, 3, 1), away: History(10, 2, 2), ou: 2.75m, ouOver: 1.95m, ouUnder: 1.85m,
            league: DnaTag.OverGoals, homeTags: DnaTag.OverGoals));

        var over = a.Picks.Single(p => p.Market == Market.OverUnder);
        Assert.Equal(PickSide.Over, over.Side);
        Assert.True(over.Score >= 8, $"score {over.Score}");
        Assert.Equal(AnalysisGroup.A, over.Group);
        Assert.Contains(over.Reasons, r => r.StartsWith("DNA cùng hướng"));
        Assert.Equal(AnalysisGroup.A, a.Group);
    }

    [Fact]
    public void High_over_line_in_over_league_waits_for_in_play()
    {
        var a = MatchAnalyzer.Analyze(Input(home: History(10, 4, 1), away: History(10, 2, 3), ou: 3.25m,
            league: DnaTag.OverGoals, homeTags: DnaTag.OverGoals));

        var over = a.Picks.Single(p => p.Market == Market.OverUnder);
        Assert.Equal(PickSide.Over, over.Side);
        Assert.Equal(AnalysisGroup.B, over.Group);
        Assert.True(over.Score <= 7.5);
        Assert.Contains(a.InPlayHints, h => h.Contains("2.5–2.75"));
    }

    [Fact]
    public void Small_sample_lowers_confidence()
    {
        var a = MatchAnalyzer.Analyze(Input(home: History(2, 3, 0), away: History(2, 0, 3), ou: 2.5m));

        Assert.True(a.LowData);
        Assert.Contains(a.Picks.Single(p => p.Market == Market.OverUnder).Reasons, r => r.Contains("Mẫu dữ liệu ít"));
    }

    [Fact]
    public void Small_sample_is_capped_at_group_b()
    {
        // Cùng kịch bản đồng thuận mạnh như bài test nhóm A, nhưng mỗi đội chỉ có 2 trận trong dữ liệu.
        var a = MatchAnalyzer.Analyze(Input(home: History(2, 3, 1), away: History(2, 2, 2), ou: 2.75m, ouOver: 1.95m, ouUnder: 1.85m,
            league: DnaTag.OverGoals, homeTags: DnaTag.OverGoals));

        Assert.All(a.Picks, p => Assert.NotEqual(AnalysisGroup.A, p.Group));
        Assert.All(a.Picks, p => Assert.True(p.Score <= 7.9, $"{p.Label} {p.Score}"));
        var over = a.Picks.Single(p => p.Market == Market.OverUnder);
        Assert.Equal(2, over.Sample);
        Assert.True(over.LowSample);
        Assert.Contains(over.Reasons, r => r.Contains("tối đa nhóm B"));

        // Đủ 10 trận thì vẫn lên A (bài test ở trên) và không bị gắn ít dữ liệu.
        var full = MatchAnalyzer.Analyze(Input(home: History(10, 3, 1), away: History(10, 2, 2), ou: 2.75m, ouOver: 1.95m, ouUnder: 1.85m,
            league: DnaTag.OverGoals, homeTags: DnaTag.OverGoals));
        Assert.False(full.Picks.Single(p => p.Market == Market.OverUnder).LowSample);
    }

    // ------------------------------------------------------------ DNA đội từ file 19 giải

    [Fact]
    public void Btts_team_supports_over_goals()
    {
        var a = MatchAnalyzer.Analyze(Input(home: History(10, 3, 1), away: History(10, 2, 2), ou: 2.75m, homeTags: DnaTag.Btts));

        var over = a.Picks.Single(p => p.Market == Market.OverUnder);
        Assert.Equal(PickSide.Over, over.Side);
        Assert.Contains(over.Reasons, r => r.StartsWith("DNA cùng hướng") && r.Contains("Nhà công làm thủ phá (BTTS)"));
    }

    [Fact]
    public void Corner_conceding_team_supports_over_corners()
    {
        var a = MatchAnalyzer.Analyze(Input(home: History(10, 2, 1, cf: 7, ca: 5), away: History(10, 1, 2, cf: 4, ca: 7), corners: 9.5m,
            awayTags: DnaTag.ConcedesCorners));

        var corners = a.Picks.Single(p => p.Market == Market.CornersOverUnder);
        Assert.Equal(PickSide.Over, corners.Side);
        Assert.Contains(corners.Reasons, r => r.Contains("Khách hay xả góc / chịu góc"));
    }

    [Fact]
    public void Bully_weak_counts_only_on_deep_line_for_better_ranked_team()
    {
        var input = Input(home: History(10, 3, 0), away: History(10, 0, 2), ah: -1.25m, homeTags: DnaTag.BullyWeak,
            homeStanding: Standing(HomeId, 1, 20), awayStanding: Standing(AwayId, 16, -12));
        var ah = MatchAnalyzer.Analyze(input).Picks.Single(p => p.Market == Market.AsianHandicap);
        Assert.Contains(ah.Reasons, r => r.Contains("bắt nạt đội yếu"));

        // Kèo nông (chấp 1/4) thì không áp nhãn bắt nạt.
        var shallow = MatchAnalyzer.Analyze(Input(home: History(10, 3, 0), away: History(10, 0, 2), ah: -0.25m, homeTags: DnaTag.BullyWeak,
            homeStanding: Standing(HomeId, 1, 20), awayStanding: Standing(AwayId, 16, -12)));
        Assert.DoesNotContain(shallow.Picks.Single(p => p.Market == Market.AsianHandicap).Reasons, r => r.Contains("bắt nạt"));
    }

    [Fact]
    public void Heavy_cards_supports_over_cards()
    {
        var input = Input(homeTags: DnaTag.HeavyCards);
        var a = MatchAnalyzer.Analyze(new AnalysisInput
        {
            Fixture = input.Fixture, HomeName = input.HomeName, AwayName = input.AwayName, LeagueName = input.LeagueName, HomeDna = input.HomeDna,
            TableSize = input.TableSize, HomeHistory = input.HomeHistory, AwayHistory = input.AwayHistory, League = input.League,
            HomeChanges = [], Injuries = [], Cards = Line(Market.CardsOverUnder, 3.5m),
        });

        var cards = a.Picks.Single(p => p.Market == Market.CardsOverUnder);
        Assert.Contains(cards.Reasons, r => r.Contains("Nhà đá rát, nhiều thẻ"));
    }

    [Fact]
    public void League_handicap_tags_score_favourite_and_underdog()
    {
        // Nhà chấp sâu −1.25: giải "tín kèo trên" cộng, giải "cửa dưới hay ăn" trừ.
        AnalysisPickResult Favourite(DnaTag league) => MatchAnalyzer.Analyze(Input(home: History(10, 3, 0), away: History(10, 0, 2), ah: -1.25m, league: league,
            homeStanding: Standing(HomeId, 1, 20), awayStanding: Standing(AwayId, 16, -12))).Picks.Single(p => p.Market == Market.AsianHandicap);

        var deep = Favourite(DnaTag.BullyWeak);
        Assert.Equal(-1.25m, deep.Line);
        Assert.Contains(deep.Reasons, r => r.Contains("phân hóa lớn, tín kèo trên chấp sâu"));
        Assert.Contains(Favourite(DnaTag.UnderdogCover).Reasons, r => r.Contains("cửa dưới hay ăn (kỵ chấp sâu)"));
        Assert.Contains(Favourite(DnaTag.Pragmatic).Reasons, r => r.Contains("vua hòa, khó thắng cách biệt"));

        // Hai đội ngang nhau mà nhà cái cho khách được chấp +1.5: cửa dưới được cộng ở giải đồng đều.
        var dog = MatchAnalyzer.Analyze(Input(home: History(10, 1, 1), away: History(10, 1, 1), ah: -1.5m, league: DnaTag.UnderdogCover))
            .Picks.Single(p => p.Market == Market.AsianHandicap);
        Assert.Equal(PickSide.Away, dog.Side);
        Assert.Contains(dog.Reasons, r => r.Contains("trình độ đồng đều, cửa dưới hay ăn"));
    }

    [Fact]
    public void League_draw_and_cards_tags_score_totals()
    {
        var under = MatchAnalyzer.Analyze(Input(home: History(10, 1, 0), away: History(10, 0, 1), ou: 2.5m, league: DnaTag.Pragmatic))
            .Picks.Single(p => p.Market == Market.OverUnder);
        Assert.Equal(PickSide.Under, under.Side);
        Assert.Contains(under.Reasons, r => r.Contains("vua hòa, thắng sát nút"));

        var input = Input(league: DnaTag.HeavyCards);
        var a = MatchAnalyzer.Analyze(new AnalysisInput
        {
            Fixture = input.Fixture, HomeName = input.HomeName, AwayName = input.AwayName, LeagueName = input.LeagueName, LeagueDna = input.LeagueDna,
            TableSize = input.TableSize, HomeHistory = input.HomeHistory, AwayHistory = input.AwayHistory, League = input.League,
            HomeChanges = [], Injuries = [], Cards = Line(Market.CardsOverUnder, 3.5m),
        });
        Assert.Contains(a.Picks.Single(p => p.Market == Market.CardsOverUnder).Reasons, r => r.Contains("giải mưa thẻ"));
        Assert.Equal("Mưa thẻ (thẻ đỏ, pen)", a.CardsRow.LeagueDnaText);
    }

    [Fact]
    public void League_profile_averages_fill_in_when_database_is_thin()
    {
        var thin = new LeagueBaseline(5, 3.5, 0, null, null) { ProfileGoals = 1.96, ProfileCorners = 8.4, ProfileCards = 5.1 };
        Assert.Equal(1.96, thin.Goals);
        Assert.Equal(8.4, thin.Corners);
        Assert.Equal(5.1, thin.Cards);
        Assert.True(thin.GoalsFromProfile);

        var full = new LeagueBaseline(30, 3.5, 30, 9.0, 4.0) { ProfileGoals = 1.96, ProfileCorners = 8.4, ProfileCards = 5.1 };
        Assert.Equal(3.5, full.Goals);
        Assert.False(full.CornersFromProfile);
        Assert.Equal(LeagueBaseline.DefaultGoals, LeagueBaseline.Empty.Goals);

        var input = Input(ou: 2.5m);
        var a = MatchAnalyzer.Analyze(new AnalysisInput
        {
            Fixture = input.Fixture, HomeName = input.HomeName, AwayName = input.AwayName, LeagueName = input.LeagueName,
            TableSize = input.TableSize, HomeHistory = input.HomeHistory, AwayHistory = input.AwayHistory, League = thin,
            HomeChanges = [], Injuries = [], Totals = input.Totals,
        });
        Assert.Equal(1.96, a.Goals.LeagueAverage);
        Assert.Contains("hồ sơ 1 năm", a.Goals.LeagueDnaText);
    }

    [Fact]
    public void League_tag_labels_cover_all_eleven_traits()
    {
        Assert.Equal(11, DnaTagText.LeagueTags.Length);
        Assert.Equal(11, DnaTagText.LeagueTags.Select(DnaTagText.ShortLeague).Distinct().Count());
        Assert.Equal("Vua hòa / thắng sát nút", DnaTagText.Label(DnaTag.Pragmatic, false));
        Assert.Equal("Thực dụng / phá lối chơi", DnaTagText.Label(DnaTag.Pragmatic, true));
        Assert.Contains(MatchAnalyzer.Analyze(Input(league: DnaTag.FastStart)).InPlayHints, h => h.StartsWith("Giải hay nổ sớm hiệp 1"));
    }

    [Fact]
    public void Fast_start_and_late_goal_teams_add_in_play_hints()
    {
        var a = MatchAnalyzer.Analyze(Input(homeTags: DnaTag.FastStart, awayTags: DnaTag.LateGoals));

        Assert.Contains(a.InPlayHints, h => h.StartsWith("Nhà đánh phủ đầu"));
        Assert.Contains(a.InPlayHints, h => h.StartsWith("Khách hay bùng nổ cuối trận"));
    }

    [Fact]
    public void Team_tag_labels_cover_all_eleven_traits()
    {
        Assert.Equal(11, DnaTagText.TeamTags.Length);
        Assert.Equal("Bùng nổ cuối trận (H2)", DnaTagText.Label(DnaTag.LateGoals, true));
        Assert.Equal("Bàn muộn (đuối sức cuối trận)", DnaTagText.Label(DnaTag.LateGoals, false));
        Assert.All(DnaTagText.TeamTags, t => Assert.DoesNotContain(t.ToString(), DnaTagText.Short(t)));
    }

    [Fact]
    public void Corner_picks_need_five_matches_with_corner_stats_per_team()
    {
        // Đội mới chỉ có 2 trận có thống kê góc: không được khuyến nghị cửa dưới chấp góc sâu "thắng 90%".
        var thin = MatchAnalyzer.Analyze(Input(home: History(2, 2, 0, cf: 8, ca: 2), away: History(2, 0, 2, cf: 2, ca: 8),
            corners: 9.5m, cornersHandicap: -3.5m));
        Assert.DoesNotContain(thin.Picks, p => p.Market is Market.CornersOverUnder or Market.CornersHandicap);
        Assert.Contains("Chưa đủ thống kê phạt góc", thin.CornerHandicap.Verdict);
        Assert.Contains("Nhà 2 / Khách 2 trận", thin.CornersRow.Verdict);

        var full = MatchAnalyzer.Analyze(Input(home: History(10, 2, 0, cf: 8, ca: 2), away: History(10, 0, 2, cf: 2, ca: 8),
            corners: 9.5m, cornersHandicap: -3.5m));
        Assert.Contains(full.Picks, p => p.Market == Market.CornersHandicap);
    }

    [Fact]
    public void Expected_goals_blend_xg_when_five_matches_have_xg()
    {
        static List<TeamMatch> WithXg(int count, decimal xgFor, decimal xgAgainst) =>
            History(10, 1, 1).Select((m, i) => i < count ? m with { XgFor = xgFor, XgAgainst = xgAgainst } : m).ToList();

        var plain = MatchAnalyzer.Analyze(Input(home: History(10, 1, 1), away: History(10, 1, 1), ou: 2.5m));
        Assert.Equal(2.0, plain.Goals.Expected!.Value, 6);
        Assert.DoesNotContain("xG", plain.Goals.Verdict);

        // Nhà: 50% × (1, 1) + 50% × xG (2, 0.5) = ghi 1.5 / thủng 0.75; khách giữ 1 / 1 → (1.5 + 1) / 2 + (1 + 0.75) / 2 = 2.125.
        var blended = MatchAnalyzer.Analyze(Input(home: WithXg(10, 2.0m, 0.5m), away: WithXg(6, 1.0m, 1.0m), ou: 2.5m));
        Assert.Equal(2.125, blended.Goals.Expected!.Value, 6);
        Assert.Contains("xG 2/0.5 (10 trận)", blended.Goals.HomeText);
        Assert.Contains("50% xG", blended.Goals.Verdict);
        Assert.True(MatchAnalyzer.UsesXg(blended.AwayForm));

        // Chỉ 3 trận có xG: chưa dùng, kỳ vọng như không có xG.
        var thin = MatchAnalyzer.Analyze(Input(home: WithXg(3, 2.0m, 0.5m), away: History(10, 1, 1), ou: 2.5m));
        Assert.Equal(2.0, thin.Goals.Expected!.Value, 6);
        Assert.Contains("(3 trận)", thin.Goals.HomeText);
        Assert.DoesNotContain("xG", thin.Goals.Verdict);
    }

    [Fact]
    public void Model_is_blended_with_market_and_capped_when_it_disagrees_on_thin_sample()
    {
        // 5 trận đầu mùa, hai đội nổ nhiều bàn → mô hình cho Tài 2.5 rất cao; nhà cái 1.85/1.95 chỉ ~51%.
        var a = MatchAnalyzer.Analyze(Input(home: History(5, 3, 2), away: History(5, 3, 2), ou: 2.5m, ouOver: 1.85m, ouUnder: 1.95m));
        var over = a.Picks.Single(p => p.Market == Market.OverUnder);
        Assert.Equal(PickSide.Over, over.Side);
        Assert.True(over.ModelProbability!.Share > 0.75);

        var q = 1 / 1.85 / (1 / 1.85 + 1 / 1.95);
        Assert.Equal(q, over.MarketShare!.Value, 6);
        Assert.Equal(q + MatchAnalyzer.ModelWeight(5) * (over.ModelProbability.Share!.Value - q), over.Probability.Share!.Value, 6);
        Assert.Equal(over.ModelProbability.Push, over.Probability.Push, 9);
        Assert.Equal(1.0, over.Probability.Win + over.Probability.HalfWin + over.Probability.Push + over.Probability.HalfLoss + over.Probability.Loss, 9);
        Assert.NotEqual(AnalysisGroup.A, over.Group);
        Assert.Contains(over.Reasons, r => r.StartsWith("Giá nhà cái (đã bỏ lời)"));
        Assert.Contains(over.Reasons, r => r.Contains("lệch giá nhà cái"));
        Assert.Equal(0.45, MatchAnalyzer.ModelWeight(5), 9);
        Assert.Equal(MatchAnalyzer.ModelWeightMax, MatchAnalyzer.ModelWeight(30), 9);
    }

    [Fact]
    public void Unmoved_line_anomaly_only_blocks_the_favoured_side()
    {
        // Mô hình: nhà chấp 1/4; nhà cái: nhà chấp 1.25 nhưng kèo không dịch → chỉ nghi cửa Nhà, cửa Khách +1.25 và tài xỉu vẫn xét.
        var a = MatchAnalyzer.Analyze(Input(home: History(10, 1, 1), away: History(10, 1, 1), ah: -1.25m, ou: 2.5m));
        var flag = a.Flags.Single(f => f.Number == 2);
        Assert.True(flag.Covers(Market.AsianHandicap, PickSide.Home));
        Assert.False(flag.Covers(Market.AsianHandicap, PickSide.Away));
        Assert.False(flag.Covers(Market.OverUnder, PickSide.Over));

        var ah = a.Picks.Single(p => p.Market == Market.AsianHandicap);
        Assert.Equal(PickSide.Away, ah.Side);
        Assert.Empty(ah.Flags);
        Assert.NotEqual(AnalysisGroup.A, ah.Group);
        Assert.Contains(ah.Reasons, r => r.Contains("tối đa nhóm B"));

        // Kèo đã nhảy 0.5 từ lúc mở mà không có lý do: vẫn loại cả kèo chấp lẫn tài xỉu như cũ, điểm hiển thị ≤ 5.9.
        var moved = MatchAnalyzer.Analyze(Input(home: History(10, 1, 1), away: History(10, 1, 1), ah: -1.25m, ahOpen: -0.75m, ou: 2.5m));
        Assert.Contains(moved.Flags, f => f.Number == 2 && f.Covers(Market.OverUnder, PickSide.Over));
        Assert.All(moved.Picks.Where(p => p.Flags.Count > 0), p => Assert.True(p.Score <= 5.9));
    }

    [Fact]
    public void Thin_sample_and_cup_matches_are_not_called_odd_lines()
    {
        var thin = MatchAnalyzer.Analyze(Input(home: History(3, 1, 1), away: History(3, 1, 1), ah: -1.25m));
        Assert.DoesNotContain(thin.Flags, f => f.Number == 2);
        Assert.Contains("chỉ dựa trên 3 trận", thin.Handicap.Summary);

        var cup = MatchAnalyzer.Analyze(Input(home: History(10, 1, 1), away: History(10, 1, 1), ah: -1.25m, leagueType: "Cup"));
        Assert.DoesNotContain(cup.Flags, f => f.Number == 2);
        Assert.Contains("Trận cúp", cup.Handicap.Summary);
        Assert.Contains(cup.Picks.Single(p => p.Market == Market.AsianHandicap).Reasons, r => r.StartsWith("Trận cúp"));
    }

    [Fact]
    public void Long_gap_rest_days_are_unknown_and_missing_league_stats_are_marked()
    {
        Assert.Null(MatchAnalyzer.Analyze(Input(home: History(10, 1, 1, startDaysBefore: 30))).HomeRestDays);
        Assert.Equal(7, MatchAnalyzer.Analyze(Input()).HomeRestDays!.Value, 6);

        var input = Input(corners: 9.5m);
        var a = MatchAnalyzer.Analyze(new AnalysisInput
        {
            Fixture = input.Fixture, HomeName = input.HomeName, AwayName = input.AwayName, TableSize = input.TableSize,
            HomeHistory = input.HomeHistory, AwayHistory = input.AwayHistory, League = LeagueBaseline.Empty,
            HomeChanges = [], Injuries = [], Corners = input.Corners,
        });
        Assert.Null(a.CornersRow.LeagueAverage);
        Assert.Contains("chưa có số liệu của giải", a.CornersRow.LeagueDnaText);
        Assert.Null(a.CardsRow.LeagueAverage);
    }

    [Fact]
    public void Red_flag_names_for_cards()
    {
        Assert.Equal("Kèo lạ / xung đột tín hiệu", RedFlagText.Short(2));
        Assert.All(Enumerable.Range(1, 5), n => Assert.DoesNotContain("Cờ", RedFlagText.Short(n)));
        Assert.Equal([2, 5], RedFlagText.Parse("2,5"));
        Assert.Empty(RedFlagText.Parse(null));
        Assert.Equal("cờ đỏ 2, 5: Kèo lạ / xung đột tín hiệu; Chờ ổn định (HLV mới / tân binh)", RedFlagText.Describe([5, 2, 2]));
    }

    // ------------------------------------------------------------ Phạt góc

    [Fact]
    public void Corner_totals_and_corner_handicap_rules()
    {
        var a = MatchAnalyzer.Analyze(Input(home: History(10, 2, 1, cf: 7, ca: 3), away: History(10, 1, 2, cf: 4, ca: 6),
            corners: 8.5m, cornersHandicap: -1.5m));

        // Kỳ vọng ((7 + 6) + (4 + 3)) / 2 = 10, mốc 8.5 → Δ +1.5 và đội nhà ép sân (7 góc/trận).
        Assert.Equal(10.0, a.CornersRow.Expected!.Value, 6);
        Assert.Equal(PickSide.Over, a.CornersRow.Lean);
        Assert.Contains("ép sân", a.CornersRow.Verdict);
        Assert.Equal(3.0, a.CornerHandicap.Expected!.Value, 6);
        Assert.Equal(PickSide.Home, a.CornerHandicap.Lean);

        var cornerPick = a.Picks.Single(p => p.Market == Market.CornersOverUnder);
        Assert.Equal(PickSide.Over, cornerPick.Side);
        Assert.True(cornerPick.ModelProbability!.Win > 0.6);
        // Đã trộn với giá nhà cái 1.90/1.90 (50%): thấp hơn mô hình nhưng vẫn trên 50%.
        Assert.InRange(cornerPick.Probability.Share!.Value, 0.5, cornerPick.ModelProbability.Share!.Value);
        Assert.Equal(PickSide.Home, a.Picks.Single(p => p.Market == Market.CornersHandicap).Side);
    }

    [Fact]
    public void Corner_market_ignores_money_flow_flags()
    {
        var a = MatchAnalyzer.Analyze(Input(home: History(10, 2, 1, cf: 7, ca: 3), away: History(10, 1, 2, cf: 4, ca: 6),
            ah: -0.25m, ou: 2.5m, corners: 8.5m,
            homeStanding: Standing(HomeId, 2, 10), awayStanding: Standing(AwayId, 15, -10)));

        Assert.Contains(a.Flags, f => f.Number == 2);
        Assert.Empty(a.Picks.Single(p => p.Market == Market.CornersOverUnder).Flags);
    }
}
