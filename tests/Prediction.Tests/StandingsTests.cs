using Prediction.Core.Engine;
using static Prediction.Tests.TestData;

namespace Prediction.Tests;

public class StandingsTests
{
    [Fact]
    public void Snapshot_only_uses_results_known_before_kickoff()
    {
        var fixtures = new[]
        {
            Match(1, 0, 10, 20, 3, 0),
            Match(2, 0, 30, 40, 1, 1),
            // Cùng ngày với trận 1, chỉ cách 1 giờ: chưa biết kết quả trận 1.
            Match(3, 0, 10, 30),
            Match(4, 7, 10, 40),
        };
        fixtures[2].KickoffUtc = Start.AddHours(1);

        var timeline = StandingsTimeline.Build(fixtures);

        Assert.Equal(0, timeline[3].Home.Played);
        Assert.Equal(1, timeline[4].Home.Played);
        Assert.Equal(3, timeline[4].Home.Points);
        Assert.Equal(1, timeline[4].Home.Rank);
        Assert.Equal("W", timeline[4].Home.Form);
        Assert.Equal("D", timeline[4].Away.Form);
    }

    [Fact]
    public void Ranks_by_points_then_goal_difference_then_goals()
    {
        var fixtures = new[]
        {
            Match(1, 0, 10, 20, 1, 0), // 10: 3đ, +1
            Match(2, 0, 30, 40, 3, 2), // 30: 3đ, +1, ghi 3
            Match(3, 0, 50, 60, 2, 0), // 50: 3đ, +2
        };

        var table = StandingsTimeline.CurrentTable(fixtures);

        Assert.Equal([50, 30, 10], table.Take(3).Select(r => r.TeamId));
        Assert.Equal(6, table.Count);
    }

    [Fact]
    public void Knockout_matches_have_no_rank_but_keep_form()
    {
        var fixtures = new[]
        {
            Match(1, 0, 10, 20, 2, 0),
            Match(2, 30, 10, 20, hasTable: false),
        };

        var timeline = StandingsTimeline.Build(fixtures);

        Assert.Null(timeline[2].Home.Rank);
        Assert.Equal("W", timeline[2].Home.Form);
    }

    [Fact]
    public void Form_keeps_last_five_and_average_goals()
    {
        var fixtures = Enumerable.Range(0, 7).Select(i => Match(i + 1, i * 7, 10, 20 + i, i % 2 == 0 ? 2 : 0, 1)).ToList();
        fixtures.Add(Match(100, 60, 10, 99));

        var snapshot = StandingsTimeline.Build(fixtures)[100].Home;

        Assert.Equal("WLWLW", snapshot.Form);
        Assert.Equal(9, snapshot.FormPoints);
        Assert.Equal(2.2m, snapshot.AvgTotalGoals);
    }
}
