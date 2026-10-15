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

    /// <summary>Mùa hiện tại theo cách đánh số của API-Football (thường là năm bắt đầu mùa, xem <see cref="SeasonYearIsEndYear"/>).</summary>
    public int CurrentSeason { get; set; }

    /// <summary>
    /// API đánh số mùa theo năm kết thúc (vd V-League: mùa 2026/27 là 2027) thay vì năm bắt đầu.
    /// </summary>
    public bool SeasonYearIsEndYear { get; set; }

    /// <summary>Mùa giải gói trong một năm dương lịch (MLS, Brazil...).</summary>
    public bool SeasonIsCalendarYear { get; set; }

    public bool IsTracked { get; set; } = true;

    /// <summary>API có thống kê trận (góc, thẻ, sút) cho mùa hiện tại.</summary>
    public bool HasStatsCoverage { get; set; } = true;

    /// <summary>API có dữ liệu chấn thương/treo giò cho mùa hiện tại.</summary>
    public bool HasInjuriesCoverage { get; set; } = true;

    public bool IsDemo { get; set; }

    /// <summary>Nhãn mùa dễ đọc, vd "2026/27".</summary>
    public string SeasonLabel(int season) => SeasonIsCalendarYear ? season.ToString() : SeasonText.Label(season, SeasonYearIsEndYear);
}

public static class SeasonText
{
    public static string Label(int season, bool yearIsEndYear = false)
    {
        var start = yearIsEndYear ? season - 1 : season;
        return $"{start}/{(start + 1) % 100:00}";
    }
}
