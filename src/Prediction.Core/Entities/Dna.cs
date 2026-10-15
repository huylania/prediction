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
}

public static class DnaTagText
{
    public static readonly DnaTag[] LeagueTags = [DnaTag.OverGoals, DnaTag.UnderGoals, DnaTag.HomeFortress, DnaTag.OverCorners, DnaTag.UnderCorners, DnaTag.LateGoals];

    public static readonly DnaTag[] TeamTags = [DnaTag.OverGoals, DnaTag.HomeFortress, DnaTag.AwayFragile, DnaTag.OverCorners, DnaTag.Pragmatic];

    public static string Label(DnaTag tag, bool team) => tag switch
    {
        DnaTag.OverGoals => team ? "Cỗ máy nổ Tài" : "Tài bàn thắng",
        DnaTag.UnderGoals => "Xỉu bàn thắng",
        DnaTag.HomeFortress => team ? "Mạnh sân nhà" : "Thánh địa sân nhà",
        DnaTag.AwayFragile => "Yếu sân khách",
        DnaTag.OverCorners => team ? "Cày góc" : "Tài góc",
        DnaTag.UnderCorners => "Xỉu góc",
        DnaTag.LateGoals => "Bàn muộn (đuối sức cuối trận)",
        DnaTag.Pragmatic => "Thực dụng / phá lối chơi",
        _ => tag.ToString(),
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

    /// <summary>"Khôn nhà dại chợ": mạnh sân nhà, yếu sân khách.</summary>
    public bool IsHomeStrongAwayWeak => Tags.HasFlag(DnaTag.HomeFortress) && Tags.HasFlag(DnaTag.AwayFragile);
}
