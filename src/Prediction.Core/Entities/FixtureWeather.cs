namespace Prediction.Core.Entities;

/// <summary>Dự báo thời tiết tại sân vào giờ bóng lăn.</summary>
public class FixtureWeather
{
    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public double? TemperatureC { get; set; }

    public int? HumidityPercent { get; set; }

    /// <summary>Lượng mưa trong giờ bóng lăn (mm).</summary>
    public double? PrecipitationMm { get; set; }

    public int? PrecipitationProbability { get; set; }

    public double? WindKph { get; set; }

    /// <summary>Mã thời tiết WMO (Open-Meteo).</summary>
    public int? WeatherCode { get; set; }

    public string Source { get; set; } = "Open-Meteo";

    public DateTime FetchedAtUtc { get; set; }
}
