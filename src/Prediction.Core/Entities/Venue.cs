namespace Prediction.Core.Entities;

/// <summary>Sân vận động: mặt sân (API-Football), tọa độ và độ cao (Open-Meteo).</summary>
public class Venue
{
    /// <summary>Id sân trên API-Football.</summary>
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? City { get; set; }

    public string? Country { get; set; }

    public int? Capacity { get; set; }

    /// <summary>"grass", "artificial turf"... theo API.</summary>
    public string? Surface { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Độ cao so với mực nước biển (m).</summary>
    public int? ElevationM { get; set; }

    /// <summary>Nguồn độ cao/tọa độ: "Open-Meteo", "AI" hoặc "Nhập tay".</summary>
    public string? GeoSource { get; set; }

    public DateTime? GeoCheckedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public bool IsArtificial => Surface?.Contains("artificial", StringComparison.OrdinalIgnoreCase) == true;
}
