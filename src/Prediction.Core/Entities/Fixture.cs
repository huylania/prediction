namespace Prediction.Core.Entities;

public class Fixture
{
    /// <summary>Id trận trên API-Football.</summary>
    public int Id { get; set; }

    public int LeagueId { get; set; }
    public League? League { get; set; }

    public int Season { get; set; }

    public string? Round { get; set; }

    public DateTime KickoffUtc { get; set; }

    /// <summary>Mã trạng thái ngắn của API-Football: NS, 1H, HT, 2H, FT, AET, PEN, PST, CANC...</summary>
    public string Status { get; set; } = FixtureStatus.NotStarted;

    public int HomeTeamId { get; set; }
    public Team? HomeTeam { get; set; }

    public int AwayTeamId { get; set; }
    public Team? AwayTeam { get; set; }

    /// <summary>Tỉ số sau 90 phút (kèo chấp/tài xỉu chấm theo 90 phút).</summary>
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }

    /// <summary>Trận thuộc giai đoạn có bảng xếp hạng (vòng đấu thường / league phase C1).</summary>
    public bool HasTable { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; }

    public bool IsFinished => FixtureStatus.IsFinished(Status) && HomeGoals.HasValue && AwayGoals.HasValue;

    public bool IsCancelled => FixtureStatus.IsCancelled(Status);
}

public static class FixtureStatus
{
    public const string NotStarted = "NS";
    public const string FullTime = "FT";

    private static readonly HashSet<string> Finished = ["FT", "AET", "PEN"];
    private static readonly HashSet<string> Cancelled = ["CANC", "ABD", "AWD", "WO"];
    private static readonly HashSet<string> Upcoming = ["NS", "TBD"];

    public static bool IsFinished(string status) => Finished.Contains(status);

    public static bool IsCancelled(string status) => Cancelled.Contains(status);

    public static bool IsUpcoming(string status) => Upcoming.Contains(status);

    public static string Label(string status) => status switch
    {
        "NS" or "TBD" => "Chưa đá",
        "1H" or "2H" or "ET" or "BT" or "P" or "LIVE" => "Đang đá",
        "HT" => "Nghỉ giữa hiệp",
        "FT" => "Kết thúc",
        "AET" => "Kết thúc (hiệp phụ)",
        "PEN" => "Kết thúc (luân lưu)",
        "PST" => "Hoãn",
        "SUSP" or "INT" => "Tạm dừng",
        "CANC" => "Hủy",
        "ABD" => "Bỏ dở",
        "AWD" or "WO" => "Xử thắng",
        _ => status,
    };
}
