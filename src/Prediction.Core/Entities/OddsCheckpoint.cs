namespace Prediction.Core.Entities;

public enum OddsCheckpointKind
{
    /// <summary>Lúc API công bố đội hình ra sân.</summary>
    Lineups = 2,

    /// <summary>Khoảng 5 phút trước giờ bóng lăn.</summary>
    PreKickoff = 3,
}

/// <summary>Kèo chấp và tài xỉu chụp tại một mốc thời gian quan trọng của trận (kèo mở lấy từ bản ghi kèo đầu tiên).</summary>
public class OddsCheckpoint
{
    public long Id { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public OddsCheckpointKind Kind { get; set; }

    public DateTime AtUtc { get; set; }

    /// <summary>Lúc nhà cái cập nhật kèo lần cuối (theo API), để biết kèo có cũ không.</summary>
    public DateTime? BookmakerUpdatedAtUtc { get; set; }

    /// <summary>Mức chấp của đội nhà.</summary>
    public decimal? HandicapLine { get; set; }
    public decimal? HandicapHome { get; set; }
    public decimal? HandicapAway { get; set; }

    public decimal? TotalLine { get; set; }
    public decimal? Over { get; set; }
    public decimal? Under { get; set; }
}
