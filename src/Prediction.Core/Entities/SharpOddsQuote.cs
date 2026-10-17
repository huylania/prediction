namespace Prediction.Core.Entities;

/// <summary>
/// Kèo trước trận của nhà cái chuẩn (Pinnacle, gói Ultra/Mega) — bảng riêng để không lẫn vào lịch sử biến động kèo Bet365.
/// Giữ bảng mức mới nhất của mỗi trận (mọi mức, đánh dấu mức chính) và một dòng mức chính lúc website thấy lần đầu.
/// </summary>
public class SharpOddsQuote
{
    public long Id { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public int BookmakerId { get; set; }

    public Market Market { get; set; }

    /// <summary>Kèo chấp: mức chấp của đội nhà. Tài xỉu: tổng.</summary>
    public decimal Line { get; set; }

    /// <summary>Odds cửa đội nhà / cửa tài.</summary>
    public decimal PriceA { get; set; }

    /// <summary>Odds cửa đội khách / cửa xỉu.</summary>
    public decimal PriceB { get; set; }

    /// <summary>Mức chính (hai odds gần nhau nhất) của lần lấy mới nhất.</summary>
    public bool IsMain { get; set; }

    /// <summary>Mức chính lúc website thấy lần đầu (không bị thay khi lấy lại) — để biết Pinnacle dịch kèo về đâu.</summary>
    public bool IsOpening { get; set; }

    public DateTime? BookmakerUpdatedAtUtc { get; set; }

    public DateTime FetchedAtUtc { get; set; }
}
