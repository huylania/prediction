namespace Prediction.Core.Entities;

/// <summary>Cầu thủ vắng mặt / bỏ ngỏ khả năng ra sân trong một trận (API /injuries).</summary>
public class FixtureInjury
{
    public long Id { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public int TeamId { get; set; }

    public string PlayerName { get; set; } = "";

    /// <summary>"Missing Fixture" (chắc chắn vắng) hoặc "Questionable" (bỏ ngỏ).</summary>
    public string Type { get; set; } = "";

    public string? Reason { get; set; }

    public bool IsMissing => Type.Contains("Missing", StringComparison.OrdinalIgnoreCase);
}
