using Prediction.Core.Engine.Analysis;
using Prediction.Core.Entities;

namespace Prediction.Tests;

public class InPlayEngineTests
{
    private static Fixture Live(string status, int? elapsed, int hg, int ag) => new()
    {
        Id = 1,
        LeagueId = 78,
        HomeTeamId = 10,
        AwayTeamId = 20,
        Status = status,
        Elapsed = elapsed,
        HomeGoals = hg,
        AwayGoals = ag,
        KickoffUtc = new DateTime(2026, 10, 17, 14, 0, 0, DateTimeKind.Utc),
    };

    private static FixtureStats Stats(int homeShots = 0, int awayShots = 0, int homeOnTarget = 0, int awayOnTarget = 0, string? goals = null, int homeCorners = 0) => new()
    {
        FixtureId = 1,
        HomeShots = homeShots,
        AwayShots = awayShots,
        HomeShotsOnTarget = homeOnTarget,
        AwayShotsOnTarget = awayOnTarget,
        HomeCorners = homeCorners,
        AwayCorners = 0,
        GoalTimeline = goals,
    };

    private static IReadOnlyList<InPlayAlert> Run(Fixture f, FixtureStats? s = null, DnaTag league = DnaTag.None, DnaTag away = DnaTag.None,
        decimal? handicap = null, decimal? total = null, RegimeStatus? homeRegime = null) =>
        InPlayEngine.Evaluate(new InPlayInput
        {
            Fixture = f,
            Stats = s,
            HomeName = "Nhà",
            AwayName = "Khách",
            LeagueTags = league,
            AwayTags = away,
            PreMatchHandicap = handicap,
            PreMatchTotal = total,
            HomeRegime = homeRegime,
        });

    [Fact]
    public void Not_live_has_no_alerts() => Assert.Empty(Run(Live("FT", null, 1, 0), Stats(goals: "5H")));

    [Fact]
    public void Rule_1_early_goal_cancels_under()
    {
        var alert = Assert.Single(Run(Live("1H", 22, 1, 0), Stats(goals: "9H"), league: DnaTag.OverGoals), a => a.Rule == 1);
        Assert.Contains("phút 9", alert.Title);
        Assert.Contains("DNA Tài", alert.Detail);

        Assert.DoesNotContain(Run(Live("1H", 30, 1, 0), Stats(goals: "27H")), a => a.Rule == 1);
    }

    [Fact]
    public void Rule_2_over_entry_needs_high_line_goalless_and_pressure()
    {
        var busy = Stats(homeShots: 6, awayShots: 4, homeOnTarget: 2, awayOnTarget: 1);
        Assert.Contains(Run(Live("1H", 20, 0, 0), busy, league: DnaTag.OverGoals, total: 3.5m), a => a.Rule == 2 && a.Detail.Contains("2.5–2.75"));

        Assert.DoesNotContain(Run(Live("1H", 20, 0, 0), busy, league: DnaTag.OverGoals, total: 2.75m), a => a.Rule == 2);
        Assert.DoesNotContain(Run(Live("1H", 20, 0, 0), Stats(homeShots: 2, awayShots: 1), league: DnaTag.OverGoals, total: 3.5m), a => a.Rule == 2);
        Assert.DoesNotContain(Run(Live("1H", 20, 0, 0), busy, total: 3.5m), a => a.Rule == 2);
    }

    [Fact]
    public void Rule_3_new_regime_verification()
    {
        var regime = new RegimeStatus(TeamChangeKind.NewManager, "HLV X", DateTime.UtcNow.AddDays(-10), 1);

        var good = Assert.Single(Run(Live("1H", 23, 0, 0), Stats(homeShots: 7, awayShots: 2, homeOnTarget: 3, awayOnTarget: 0), homeRegime: regime), a => a.Rule == 3);
        Assert.Equal(InPlaySeverity.Opportunity, good.Severity);

        var weak = Assert.Single(Run(Live("1H", 23, 0, 0), Stats(homeShots: 3, awayShots: 5), homeRegime: regime), a => a.Rule == 3);
        Assert.Equal(InPlaySeverity.Info, weak.Severity);

        var stable = regime with { MatchesUnder = 5 };
        Assert.DoesNotContain(Run(Live("1H", 23, 0, 0), Stats(homeShots: 7, awayShots: 2), homeRegime: stable), a => a.Rule == 3);
    }

    [Fact]
    public void Rule_4_stalemate_at_half_time()
    {
        var alert = Assert.Single(Run(Live("HT", 45, 0, 0), Stats(homeShots: 8, homeOnTarget: 1, homeCorners: 6), handicap: -1m), a => a.Rule == 4);
        Assert.Equal(InPlaySeverity.Warning, alert.Severity);
        Assert.Contains("không nhồi Nhà", alert.Detail);
        Assert.Contains("kèo góc", alert.Detail);

        Assert.DoesNotContain(Run(Live("HT", 45, 0, 0), Stats(homeOnTarget: 3, awayOnTarget: 1), handicap: -1m), a => a.Rule == 4);
        // Đội khách "dại chợ" làm cửa trên mà 0-0: vẫn cảnh báo dù sút trúng đích nhiều.
        Assert.Contains(Run(Live("HT", 45, 0, 0), Stats(homeOnTarget: 3, awayOnTarget: 2), away: DnaTag.AwayFragile, handicap: 0.5m), a => a.Rule == 4);
    }

    [Fact]
    public void Rule_5_late_goals_from_minute_60()
    {
        var alert = Assert.Single(Run(Live("2H", 66, 1, 1), Stats(), league: DnaTag.LateGoals | DnaTag.HomeFortress), a => a.Rule == 5);
        Assert.Contains("chủ nhà", alert.Detail);
        Assert.DoesNotContain(Run(Live("2H", 55, 1, 1), Stats(), league: DnaTag.LateGoals), a => a.Rule == 5);
        Assert.DoesNotContain(Run(Live("2H", 66, 1, 1), Stats()), a => a.Rule == 5);
    }
}
