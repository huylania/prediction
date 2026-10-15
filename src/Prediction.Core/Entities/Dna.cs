namespace Prediction.Core.Entities;

/// <summary>Nhãn phong cách (DNA) của giải đấu hoặc đội bóng.</summary>
[Flags]
public enum DnaTag
{
    None = 0,

    /// <summary>Thiên hướng Tài bàn thắng.</summary>
    OverGoals = 1,

    /// <summary>Thiên hướng Xỉu bàn thắng / thực dụng.</summary>
    UnderGoals = 2,

    /// <summary>Giải: lợi thế sân nhà cực đoan. Đội: rất mạnh sân nhà.</summary>
    HomeFortress = 4,

    /// <summary>Đội: kém và hay thua kèo sân khách ("dại chợ").</summary>
    AwayFragile = 8,

    /// <summary>Thiên hướng Tài phạt góc (giải) / cày góc mạnh (đội).</summary>
    OverCorners = 16,

    /// <summary>Thiên hướng Xỉu phạt góc.</summary>
    UnderCorners = 32,

    /// <summary>Hàng thủ đuối sức cuối trận, hay có bàn muộn (MLS, Úc, Brazil B).</summary>
    LateGoals = 64,

    /// <summary>Đội thực dụng / phá lối chơi.</summary>
    Pragmatic = 128,

    /// <summary>Đội: bắt nạt đội yếu, thắng được kèo chấp sâu.</summary>
    BullyWeak = 256,

    /// <summary>Đội: công làm thủ phá — hay có cả hai đội ghi bàn (BTTS).</summary>
    Btts = 512,

    /// <summary>Đội: xả góc / hay phải chịu nhiều phạt góc.</summary>
    ConcedesCorners = 1024,

    /// <summary>Đội: đánh phủ đầu, hay có bàn trong hiệp 1.</summary>
    FastStart = 2048,

    /// <summary>Đội: đá rát, nhiều thẻ phạt.</summary>
    HeavyCards = 4096,
}

public static class DnaTagText
{
    public static readonly DnaTag[] LeagueTags = [DnaTag.OverGoals, DnaTag.UnderGoals, DnaTag.HomeFortress, DnaTag.OverCorners, DnaTag.UnderCorners, DnaTag.LateGoals];

    public static readonly DnaTag[] TeamTags =
    [
        DnaTag.OverGoals, DnaTag.HomeFortress, DnaTag.AwayFragile, DnaTag.OverCorners, DnaTag.Pragmatic, DnaTag.BullyWeak,
        DnaTag.Btts, DnaTag.ConcedesCorners, DnaTag.FastStart, DnaTag.LateGoals, DnaTag.HeavyCards,
    ];

    public static string Label(DnaTag tag, bool team) => tag switch
    {
        DnaTag.OverGoals => team ? "Cỗ máy nổ Tài" : "Tài bàn thắng",
        DnaTag.UnderGoals => "Xỉu bàn thắng",
        DnaTag.HomeFortress => team ? "Mạnh sân nhà" : "Thánh địa sân nhà",
        DnaTag.AwayFragile => "Yếu sân khách",
        DnaTag.OverCorners => team ? "Cày góc" : "Tài góc",
        DnaTag.UnderCorners => "Xỉu góc",
        DnaTag.LateGoals => team ? "Bùng nổ cuối trận (H2)" : "Bàn muộn (đuối sức cuối trận)",
        DnaTag.Pragmatic => "Thực dụng / phá lối chơi",
        DnaTag.BullyWeak => "Bắt nạt đội yếu / chấp sâu",
        DnaTag.Btts => "Công làm thủ phá (BTTS)",
        DnaTag.ConcedesCorners => "Xả góc / chịu góc",
        DnaTag.FastStart => "Đánh phủ đầu (Tài H1)",
        DnaTag.HeavyCards => "Đá rát / nhiều thẻ",
        _ => tag.ToString(),
    };

    /// <summary>Tên ngắn cho tiêu đề cột trên trang DNA (đội).</summary>
    public static string Short(DnaTag tag) => tag switch
    {
        DnaTag.OverGoals => "Nổ Tài",
        DnaTag.HomeFortress => "Mạnh nhà",
        DnaTag.AwayFragile => "Yếu khách",
        DnaTag.OverCorners => "Cày góc",
        DnaTag.Pragmatic => "Thực dụng",
        DnaTag.BullyWeak => "Bắt nạt",
        DnaTag.Btts => "BTTS",
        DnaTag.ConcedesCorners => "Xả góc",
        DnaTag.FastStart => "Phủ đầu H1",
        DnaTag.LateGoals => "Bùng nổ H2",
        DnaTag.HeavyCards => "Rát thẻ",
        _ => Label(tag, true),
    };

    public static IEnumerable<string> Labels(DnaTag tags, bool team) =>
        Enum.GetValues<DnaTag>().Where(t => t != DnaTag.None && tags.HasFlag(t)).Select(t => Label(t, team));
}

public class LeagueDna
{
    /// <summary>Id giải trên API-Football (có thể là giải chưa theo dõi).</summary>
    public int LeagueId { get; set; }

    public string LeagueName { get; set; } = "";

    public DnaTag Tags { get; set; }

    public string? Notes { get; set; }
}

public class TeamDna
{
    /// <summary>Id đội trên API-Football.</summary>
    public int TeamId { get; set; }

    public string TeamName { get; set; } = "";

    public DnaTag Tags { get; set; }

    public string? Notes { get; set; }

    /// <summary>Hồ sơ 1 năm (từ file DNA nhập vào): giải lấy số liệu.</summary>
    public int? SourceLeagueId { get; set; }

    public string? SourceLeagueName { get; set; }

    /// <summary>Số trận trong 1 năm làm mẫu.</summary>
    public int? Matches { get; set; }

    public decimal? AvgTotalGoals { get; set; }
    public decimal? AvgGoalsFor { get; set; }
    public decimal? AvgGoalsAgainst { get; set; }

    /// <summary>% trận Tài 2.5.</summary>
    public decimal? Over25Pct { get; set; }

    /// <summary>% trận hai đội cùng ghi bàn.</summary>
    public decimal? BttsPct { get; set; }

    public decimal? HomeWinPct { get; set; }
    public decimal? AwayLossPct { get; set; }
    public decimal? AvgCornersFor { get; set; }
    public decimal? AvgCornersAgainst { get; set; }

    /// <summary>% trận có bàn trong hiệp 1.</summary>
    public decimal? FirstHalfGoalPct { get; set; }

    public decimal? AvgSecondHalfGoals { get; set; }
    public decimal? AvgCards { get; set; }

    /// <summary>Tóm tắt đặc điểm (ghi chú của file DNA).</summary>
    public string? Summary { get; set; }

    /// <summary>Phân tích lý do chọn từng đặc điểm.</summary>
    public string? Analysis { get; set; }

    public DateTime? ProfileUpdatedAtUtc { get; set; }

    /// <summary>Các giải đội có số liệu trong file DNA (quốc nội + cúp châu lục), để lọc theo giải.</summary>
    public List<TeamDnaLeague> Leagues { get; set; } = [];

    /// <summary>"Khôn nhà dại chợ": mạnh sân nhà, yếu sân khách.</summary>
    public bool IsHomeStrongAwayWeak => Tags.HasFlag(DnaTag.HomeFortress) && Tags.HasFlag(DnaTag.AwayFragile);
}

/// <summary>
/// Một giải mà đội có dòng số liệu trong file DNA. Hồ sơ 1 năm chỉ lấy 1 giải (nhiều trận nhất),
/// nhưng đội vẫn thuộc mọi giải trong file — vd Arsenal: Ngoại hạng Anh + Champions League.
/// </summary>
public class TeamDnaLeague
{
    public int TeamId { get; set; }

    public int LeagueId { get; set; }

    public string LeagueName { get; set; } = "";

    /// <summary>Số trận của đội ở giải này trong file (1 năm).</summary>
    public int? Matches { get; set; }
}
