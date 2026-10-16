namespace Prediction.Core.Entities;

/// <summary>Thông tin bên lề do AI (Claude + tra cứu web) điền: thù hận, động lực, tin lề, đặc điểm sân.</summary>
public class MatchSideInfo
{
    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    /// <summary>Lịch sử thù hận / derby / đối đầu đáng chú ý.</summary>
    public string? Rivalry { get; set; }

    public string? Motivation { get; set; }

    /// <summary>Tin bên lề: lực lượng, nội bộ, lịch thi đấu, phát biểu...</summary>
    public string? News { get; set; }

    public string? VenueNotes { get; set; }

    public string? Weather { get; set; }

    public int? AltitudeM { get; set; }

    public string? Surface { get; set; }

    /// <summary>Các nguồn web, mỗi dòng một đường dẫn.</summary>
    public string? Sources { get; set; }

    public string Model { get; set; } = "";

    public decimal CostUsd { get; set; }

    /// <summary>true nếu chạy tự động (trận nhóm A/B), false nếu bấm tay.</summary>
    public bool IsAuto { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Bản cũ của thông tin bên lề, chép lại trước khi "Điền lại" ghi đè (để so sánh, không mất tiền đã trả).</summary>
public class MatchSideInfoVersion
{
    public long Id { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public string? Rivalry { get; set; }
    public string? Motivation { get; set; }
    public string? News { get; set; }
    public string? VenueNotes { get; set; }
    public string? Weather { get; set; }
    public int? AltitudeM { get; set; }
    public string? Surface { get; set; }
    public string? Sources { get; set; }
    public string Model { get; set; } = "";
    public decimal CostUsd { get; set; }
    public bool IsAuto { get; set; }

    /// <summary>Lúc AI điền bản này.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Lúc bản này bị thay bằng bản mới.</summary>
    public DateTime ReplacedAtUtc { get; set; }

    public static MatchSideInfoVersion Of(MatchSideInfo info, DateTime replacedAtUtc) => new()
    {
        FixtureId = info.FixtureId,
        Rivalry = info.Rivalry,
        Motivation = info.Motivation,
        News = info.News,
        VenueNotes = info.VenueNotes,
        Weather = info.Weather,
        AltitudeM = info.AltitudeM,
        Surface = info.Surface,
        Sources = info.Sources,
        Model = info.Model,
        CostUsd = info.CostUsd,
        IsAuto = info.IsAuto,
        CreatedAtUtc = info.CreatedAtUtc,
        ReplacedAtUtc = replacedAtUtc,
    };
}
