namespace Prediction.Core.Entities;

public class Team
{
    /// <summary>Id đội trên API-Football.</summary>
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? LogoUrl { get; set; }

    /// <summary>Lần cuối kiểm tra HLV qua API.</summary>
    public DateTime? CoachCheckedAtUtc { get; set; }
}
