namespace Prediction.Core.Entities;

/// <summary>Đội hình ra sân của hai đội (lấy lần đầu API công bố).</summary>
public class FixtureLineup
{
    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public DateTime DetectedAtUtc { get; set; }

    public string? HomeFormation { get; set; }
    public string? AwayFormation { get; set; }

    /// <summary>Tên 11 cầu thủ đá chính, cách nhau bởi dấu phẩy.</summary>
    public string HomeStartXI { get; set; } = "";
    public string AwayStartXI { get; set; } = "";

    public string? HomeCoach { get; set; }
    public string? AwayCoach { get; set; }
}
