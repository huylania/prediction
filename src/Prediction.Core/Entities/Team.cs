namespace Prediction.Core.Entities;

public class Team
{
    /// <summary>Id đội trên API-Football.</summary>
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? LogoUrl { get; set; }
}
