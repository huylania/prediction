using Prediction.Core.Engine.Analysis;
using Prediction.Core.Entities;
using Prediction.Web.Services;

namespace Prediction.Tests;

public class DnaVerifierTests
{
    private static List<VerifyMatch> Matches(int n, Func<int, VerifyMatch> make) => Enumerable.Range(0, n).Select(make).ToList();

    [Fact]
    public void Team_tags_are_checked_against_real_results()
    {
        // 12 trận: 6 nhà (thắng 2), 6 khách; toàn tỉ số thấp, có thống kê góc.
        var matches = Matches(12, i => i < 6
            ? new VerifyMatch(true, i < 2 ? 1 : 0, i < 2 ? 0 : 1, 0, 0, CornersFor: 3, CornersAgainst: 6, CardsFor: 2, CardsAgainst: 2)
            : new VerifyMatch(false, 1, 1, 1, 0, CornersFor: 4, CornersAgainst: 5, CardsFor: 2, CardsAgainst: 2));
        var stats = DnaVerifier.Stats(matches);
        Assert.Equal(0, stats.Over25.Value);
        Assert.Equal(100.0 * 2 / 6, stats.HomeWin.Value!.Value, 6);

        var tags = DnaTag.OverGoals | DnaTag.HomeFortress | DnaTag.OverCorners | DnaTag.ConcedesCorners | DnaTag.Pragmatic | DnaTag.BullyWeak;
        var checks = DnaVerifier.CheckTeam(tags, stats);
        DnaCheckStatus Of(DnaTag t) => checks.Single(c => c.Tag == t).Status;

        Assert.Equal(DnaCheckStatus.Contradict, Of(DnaTag.OverGoals));
        Assert.Equal(DnaCheckStatus.Contradict, Of(DnaTag.HomeFortress));
        Assert.Equal(DnaCheckStatus.Contradict, Of(DnaTag.OverCorners)); // kiếm 3.5 góc/trận
        Assert.Equal(DnaCheckStatus.Match, Of(DnaTag.ConcedesCorners)); // chịu 5.5 góc/trận
        Assert.Equal(DnaCheckStatus.Match, Of(DnaTag.Pragmatic));
        Assert.Equal(DnaCheckStatus.Untestable, Of(DnaTag.BullyWeak));
        Assert.Equal(DnaTag.OverGoals | DnaTag.HomeFortress | DnaTag.OverCorners, DnaVerifier.Contradicted(checks));
        Assert.Contains("NGƯỢC", checks.Single(c => c.Tag == DnaTag.OverGoals).Text);
    }

    [Fact]
    public void Thin_samples_and_missing_stats_are_not_judged()
    {
        var few = DnaVerifier.Stats(Matches(6, _ => new VerifyMatch(true, 0, 0)));
        var checks = DnaVerifier.CheckTeam(DnaTag.OverGoals | DnaTag.OverCorners, few);
        Assert.All(checks, c => Assert.Equal(DnaCheckStatus.NoData, c.Status));
        Assert.Equal(DnaTag.None, DnaVerifier.Contradicted(checks));
    }

    [Fact]
    public void League_tags_use_league_thresholds_and_late_goals()
    {
        // 40 trận giải: chủ nhà thắng 15 (37.5%), 20 trận có bàn sau phút 75.
        var matches = Matches(40, i => new VerifyMatch(true, i < 15 ? 2 : 1, i < 15 ? 0 : 1, 1, 0, LateGoal: i % 2 == 0));
        var checks = DnaVerifier.CheckLeague(DnaTag.HomeFortress | DnaTag.LateGoals | DnaTag.UnderdogCover, DnaVerifier.Stats(matches));
        Assert.Equal(DnaCheckStatus.Contradict, checks.Single(c => c.Tag == DnaTag.HomeFortress).Status);
        Assert.Equal(DnaCheckStatus.Match, checks.Single(c => c.Tag == DnaTag.LateGoals).Status);
        Assert.Equal(DnaCheckStatus.Untestable, checks.Single(c => c.Tag == DnaTag.UnderdogCover).Status);
    }

    [Fact]
    public void Late_goal_from_timeline()
    {
        Assert.False(DnaVerifier.LateGoal(0, null));
        Assert.True(DnaVerifier.LateGoal(2, "12H,90+2A"));
        Assert.True(DnaVerifier.LateGoal(1, "76H"));
        Assert.False(DnaVerifier.LateGoal(2, "12H,75A"));
        Assert.Null(DnaVerifier.LateGoal(1, ""));
    }

    [Fact]
    public void Sql_script_removes_only_contradicted_tags()
    {
        var stats = DnaVerifier.Stats(Matches(12, _ => new VerifyMatch(true, 0, 0)));
        var tags = DnaTag.OverGoals | DnaTag.Pragmatic;
        var team = new DnaCheckResult(42, "Đội A", tags, stats, DnaVerifier.CheckTeam(tags, stats));
        var report = new DnaCheckReport(DateTime.UtcNow, DateTime.UtcNow.AddDays(-365), 12,
            new Dictionary<int, DnaCheckResult> { [42] = team }, new Dictionary<int, DnaCheckResult>());
        var sql = DnaCheckService.SqlScript(report);
        Assert.Contains($"UPDATE dbo.TeamDnas SET Tags = Tags & ~{(int)DnaTag.OverGoals} WHERE TeamId = 42;", sql);
        Assert.Contains("Đội A: bỏ Cỗ máy nổ Tài", sql);
        Assert.DoesNotContain("LeagueDnas", sql);
    }

    [Fact]
    public void Web_search_errors_are_summarised()
    {
        Assert.Null(AiReportService.SearchErrorText([]));
        Assert.Equal("Tra cứu web lỗi: max_uses_exceeded ×2, too_many_requests", AiReportService.SearchErrorText(["max_uses_exceeded", "max_uses_exceeded", "too_many_requests"]));
    }
}
