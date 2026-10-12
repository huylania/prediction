using System.Globalization;
using Microsoft.Extensions.Options;

namespace Prediction.Web.Services;

public sealed class DisplayOptions
{
    public const string Section = "Display";

    /// <summary>Múi giờ hiển thị (IANA), mặc định giờ Việt Nam.</summary>
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
}

/// <summary>Đổi giờ UTC trong CSDL sang giờ địa phương để hiển thị.</summary>
public sealed class TimeDisplay
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private readonly TimeZoneInfo _zone;

    public TimeDisplay(IOptions<DisplayOptions> options)
    {
        try
        {
            _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            _zone = TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
        }
    }

    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);

    public DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _zone);

    public DateTime TodayLocal => ToLocal(DateTime.UtcNow).Date;

    /// <summary>"T7 18/10 21:00".</summary>
    public string Kickoff(DateTime utc)
    {
        var local = ToLocal(utc);
        return $"{DayName(local.DayOfWeek)} {local:dd/MM HH:mm}";
    }

    public string Full(DateTime utc) => ToLocal(utc).ToString("dd/MM/yyyy HH:mm", Vi);

    public string Short(DateTime utc) => ToLocal(utc).ToString("dd/MM HH:mm", Vi);

    private static string DayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "T2",
        DayOfWeek.Tuesday => "T3",
        DayOfWeek.Wednesday => "T4",
        DayOfWeek.Thursday => "T5",
        DayOfWeek.Friday => "T6",
        DayOfWeek.Saturday => "T7",
        _ => "CN",
    };
}
