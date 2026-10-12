namespace Prediction.Core.Entities;

public class League
{
    /// <summary>Id giải trên API-Football.</summary>
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? Country { get; set; }

    public string? LogoUrl { get; set; }

    /// <summary>"League" hoặc "Cup" theo API-Football.</summary>
    public string? Type { get; set; }

    /// <summary>Mùa hiện tại theo cách đánh số của API-Football (năm bắt đầu mùa).</summary>
    public int CurrentSeason { get; set; }

    public bool IsTracked { get; set; } = true;

    public bool IsDemo { get; set; }
}
