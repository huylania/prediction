namespace Prediction.Core.Entities;

/// <summary>Thống kê một trận (đang đá hoặc đã đá) lấy từ API: góc, sút, xG, thẻ, diễn biến bàn thắng.</summary>
public class FixtureStats
{
    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public int? HomeCorners { get; set; }
    public int? AwayCorners { get; set; }
    public int? HomeShots { get; set; }
    public int? AwayShots { get; set; }
    public int? HomeShotsOnTarget { get; set; }
    public int? AwayShotsOnTarget { get; set; }
    public decimal? HomeXg { get; set; }
    public decimal? AwayXg { get; set; }
    public int? HomePossession { get; set; }
    public int? AwayPossession { get; set; }
    public int? HomeYellow { get; set; }
    public int? AwayYellow { get; set; }
    public int? HomeRed { get; set; }
    public int? AwayRed { get; set; }

    public int? HalftimeHome { get; set; }
    public int? HalftimeAway { get; set; }

    /// <summary>Các bàn thắng theo thứ tự, dạng "12H,34A,90+2H" (phút + đội ghi).</summary>
    public string? GoalTimeline { get; set; }

    /// <summary>Thống kê cuối cùng của trận đã kết thúc (không cần lấy lại).</summary>
    public bool IsFinal { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public int? TotalCorners => HomeCorners + AwayCorners;

    public int? TotalCards => HomeYellow + AwayYellow + (HomeRed ?? 0) + (AwayRed ?? 0);

    /// <summary>Phút của bàn thắng đầu tiên (null nếu chưa có bàn).</summary>
    public int? FirstGoalMinute => Goals().Select(g => (int?)g.Minute).FirstOrDefault();

    public IEnumerable<(int Minute, bool Home)> Goals()
    {
        if (string.IsNullOrWhiteSpace(GoalTimeline))
        {
            yield break;
        }

        foreach (var part in GoalTimeline.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var side = part[^1];
            var minuteText = part[..^1].Split('+')[0];
            if (int.TryParse(minuteText, out var minute))
            {
                yield return (minute, side == 'H');
            }
        }
    }
}
