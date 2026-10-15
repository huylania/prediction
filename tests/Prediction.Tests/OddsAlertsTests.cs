using Prediction.Core.Engine.Analysis;
using Prediction.Core.Entities;

namespace Prediction.Tests;

public class OddsAlertsTests
{
    private static readonly DateTime T = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private static LinePoint P(string label, decimal? ah, decimal? ou) => new(label, T, ah, 1.9m, 1.9m, ou, 1.9m, 1.9m);

    [Fact]
    public void Movement_of_half_ball_from_open_is_flagged()
    {
        var alerts = OddsAlerts.Movement(P("Mở kèo", -0.5m, 2.5m), P("Khi có đội hình", -0.75m, 2.5m), P("5 phút trước trận", -1m, 3m), null, "MU", "Leeds");

        var ah = Assert.Single(alerts, a => a.Market == Market.AsianHandicap);
        Assert.Equal(-0.5m, ah.Delta);
        Assert.Contains("MU -0.5 (mở kèo) → MU -1 (5 phút trước trận)", ah.Detail);
        Assert.Contains("nghiêng về MU", ah.Detail);

        var ou = alerts.Where(a => a.Market == Market.OverUnder).ToList();
        Assert.Equal(2, ou.Count); // từ lúc mở và riêng đoạn sau khi có đội hình
        Assert.All(ou, a => Assert.Equal(0.5m, a.Delta));
        Assert.Contains(ou, a => a.Title.Contains("sau khi có đội hình"));
    }

    [Fact]
    public void Quarter_moves_are_not_flagged()
    {
        Assert.Empty(OddsAlerts.Movement(P("Mở kèo", -0.5m, 2.5m), null, null, P("Hiện tại", -0.75m, 2.75m), "A", "B"));
        Assert.Empty(OddsAlerts.Movement(null, null, null, P("Hiện tại", -0.75m, 2.75m), "A", "B"));
    }

    private static HistoryLine Meeting(int daysAgo, int home, int away, decimal? ah, decimal? ou, int hg = 1, int ag = 1, LineOrigin origin = LineOrigin.Recorded) =>
        new(daysAgo, T.AddDays(-daysAgo), "EPL", home, away, home == 1 ? "MU" : "Leeds", away == 1 ? "MU" : "Leeds", hg, ag, ah, ou, origin);

    [Fact]
    public void Compares_with_last_meeting_from_todays_home_side()
    {
        // Lần gặp gần nhất Leeds làm chủ nhà, Leeds +0.75 → MU -0.75. Hôm nay MU -1.5 → lệch 0.75.
        var meetings = new[] { Meeting(100, 2, 1, 0.75m, 2.5m, origin: LineOrigin.Ai), Meeting(300, 1, 2, -1m, 2.75m) };

        var alerts = OddsAlerts.HeadToHead(meetings, 1, -1.5m, 2.5m, "MU", "Leeds");

        var last = Assert.Single(alerts, a => a.Kind == OddsAlertKind.LastMeeting);
        Assert.Equal(-0.75m, last.Delta);
        Assert.Contains("MU -0.75", last.Detail);
        Assert.Contains("đổi sân", last.Detail);
        Assert.Contains("AI tra cứu", last.Detail);
        Assert.DoesNotContain(alerts, a => a.Kind == OddsAlertKind.MeetingRange); // -0.75 và -1: biên độ 0.25
    }

    [Fact]
    public void Range_of_recent_meetings_above_half_ball()
    {
        var meetings = new[]
        {
            Meeting(10, 1, 2, -0.5m, 2.5m),
            Meeting(200, 2, 1, -0.25m, 3.25m), // MU +0.25
            Meeting(400, 1, 2, -1m, 2.5m),
        };

        var alerts = OddsAlerts.HeadToHead(meetings, 1, -0.5m, 2.75m, "MU", "Leeds");

        var ah = Assert.Single(alerts, a => a.Kind == OddsAlertKind.MeetingRange && a.Market == Market.AsianHandicap);
        Assert.Equal(1.25m, ah.Delta); // từ -1 đến +0.25
        var ou = Assert.Single(alerts, a => a.Kind == OddsAlertKind.MeetingRange && a.Market == Market.OverUnder);
        Assert.Equal(0.75m, ou.Delta);
        Assert.DoesNotContain(alerts, a => a.Kind == OddsAlertKind.LastMeeting);
    }

    [Fact]
    public void Exactly_half_ball_range_is_not_above_threshold()
    {
        var meetings = new[] { Meeting(10, 1, 2, -0.5m, null), Meeting(200, 1, 2, -1m, null) };
        Assert.Empty(OddsAlerts.HeadToHead(meetings, 1, -0.75m, null, "MU", "Leeds"));
    }

    [Fact]
    public void History_line_outcomes_from_team_side()
    {
        var m = Meeting(10, 2, 1, 0.5m, 2.5m, hg: 0, ag: 2); // Leeds +0.5 ở nhà, thua 0-2

        Assert.Equal(-0.5m, m.HandicapFor(1));
        Assert.Equal(BetOutcome.Win, m.HandicapOutcome(1));
        Assert.Equal(BetOutcome.Loss, m.HandicapOutcome(2));
        Assert.Equal(BetOutcome.Loss, m.OverOutcome);
    }

    [Fact]
    public void Conditions_notes()
    {
        var notes = MatchConditions.Notes(new Venue { ElevationM = 3640, Surface = "artificial turf" },
            new FixtureWeather { PrecipitationMm = 3.1, WindKph = 40, TemperatureC = 12 });

        Assert.Equal(4, notes.Count);
        Assert.Contains(notes, n => n.Contains("3640 m"));
        Assert.Empty(MatchConditions.Notes(new Venue { ElevationM = 90, Surface = "grass" }, new FixtureWeather { PrecipitationMm = 0.2, WindKph = 12, TemperatureC = 15 }));
    }
}
