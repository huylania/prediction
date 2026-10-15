using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Prediction.Web.Services;

public sealed class OpenMeteoOptions
{
    public const string Section = "OpenMeteo";

    /// <summary>Tắt nếu máy không ra được internet tới open-meteo.com.</summary>
    public bool Enabled { get; set; } = true;

    public string GeocodingUrl { get; set; } = "https://geocoding-api.open-meteo.com/v1/search";

    public string ForecastUrl { get; set; } = "https://api.open-meteo.com/v1/forecast";
}

public sealed record GeoPoint(double Latitude, double Longitude, int? ElevationM);

public sealed record WeatherAtKickoff(double? TemperatureC, int? HumidityPercent, double? PrecipitationMm, int? PrecipitationProbability, double? WindKph, int? WeatherCode);

/// <summary>Open-Meteo (miễn phí, không cần key): tọa độ + độ cao của thành phố và dự báo thời tiết theo giờ (tối đa 16 ngày).</summary>
public sealed class OpenMeteoClient(HttpClient http, IOptions<OpenMeteoOptions> options, ILogger<OpenMeteoClient> logger)
{
    private static readonly Dictionary<string, string> CountryAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["England"] = "United Kingdom",
        ["Scotland"] = "United Kingdom",
        ["Wales"] = "United Kingdom",
        ["Northern-Ireland"] = "United Kingdom",
        ["USA"] = "United States",
        ["Czech-Republic"] = "Czechia",
    };

    public bool Enabled => options.Value.Enabled;

    /// <summary>Tìm thành phố ("Coventry, West Midlands" → "Coventry"), ưu tiên kết quả cùng quốc gia.</summary>
    public async Task<GeoPoint?> GeocodeAsync(string city, string? country, CancellationToken ct)
    {
        var name = city.Split(',')[0].Trim();
        if (name.Length < 2)
        {
            return null;
        }

        using var doc = await GetAsync($"{options.Value.GeocodingUrl}?name={Uri.EscapeDataString(name)}&count=10&language=en&format=json", ct);
        if (doc is null || !doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var wanted = country is null ? null : CountryAliases.GetValueOrDefault(country, country.Replace('-', ' '));
        JsonElement? best = null;
        foreach (var r in results.EnumerateArray())
        {
            best ??= r;
            if (wanted is not null && r.TryGetProperty("country", out var c) && string.Equals(c.GetString(), wanted, StringComparison.OrdinalIgnoreCase))
            {
                best = r;
                break;
            }
        }

        if (best is not { } b)
        {
            return null;
        }

        return new GeoPoint(
            b.GetProperty("latitude").GetDouble(),
            b.GetProperty("longitude").GetDouble(),
            b.TryGetProperty("elevation", out var e) && e.ValueKind == JsonValueKind.Number ? (int)Math.Round(e.GetDouble()) : null);
    }

    /// <summary>Dự báo đúng giờ bóng lăn (giờ UTC tròn gần nhất).</summary>
    public async Task<WeatherAtKickoff?> ForecastAsync(double latitude, double longitude, DateTime kickoffUtc, CancellationToken ct)
    {
        var hour = new DateTime(kickoffUtc.Year, kickoffUtc.Month, kickoffUtc.Day, kickoffUtc.Hour, 0, 0, DateTimeKind.Utc);
        if (kickoffUtc.Minute >= 30)
        {
            hour = hour.AddHours(1);
        }

        var at = hour.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        var url = $"{options.Value.ForecastUrl}?latitude={latitude.ToString(CultureInfo.InvariantCulture)}&longitude={longitude.ToString(CultureInfo.InvariantCulture)}"
            + "&hourly=temperature_2m,relative_humidity_2m,precipitation,precipitation_probability,wind_speed_10m,weather_code"
            + $"&timezone=GMT&start_hour={at}&end_hour={at}";
        using var doc = await GetAsync(url, ct);
        if (doc is null || !doc.RootElement.TryGetProperty("hourly", out var hourly))
        {
            return null;
        }

        return new WeatherAtKickoff(
            First(hourly, "temperature_2m"),
            (int?)First(hourly, "relative_humidity_2m"),
            First(hourly, "precipitation"),
            (int?)First(hourly, "precipitation_probability"),
            First(hourly, "wind_speed_10m"),
            (int?)First(hourly, "weather_code"));
    }

    private static double? First(JsonElement hourly, string name) =>
        hourly.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0 && arr[0].ValueKind == JsonValueKind.Number
            ? arr[0].GetDouble()
            : null;

    private async Task<JsonDocument?> GetAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Open-Meteo trả HTTP {Status}: {Url}", (int)response.StatusCode, url);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Không gọi được Open-Meteo ({Message}). Kiểm tra mạng hoặc tắt OpenMeteo:Enabled.", ex.Message);
            return null;
        }
    }

    /// <summary>Mô tả mã thời tiết WMO.</summary>
    public static string Describe(int? code) => code switch
    {
        null => "",
        0 => "trời quang",
        1 or 2 => "ít mây",
        3 => "nhiều mây",
        45 or 48 => "sương mù",
        >= 51 and <= 57 => "mưa phùn",
        >= 61 and <= 65 => "mưa",
        66 or 67 => "mưa băng",
        >= 71 and <= 77 => "tuyết",
        >= 80 and <= 82 => "mưa rào",
        85 or 86 => "mưa tuyết",
        >= 95 => "dông",
        _ => $"mã {code}",
    };
}
