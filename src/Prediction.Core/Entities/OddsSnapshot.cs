namespace Prediction.Core.Entities;

/// <summary>
/// Một lần chào kèo. Chỉ ghi khi mức kèo hoặc tiền kèo khác bản trước,
/// nên chuỗi snapshot của một trận chính là lịch sử biến động kèo.
/// </summary>
public class OddsSnapshot
{
    public long Id { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public Market Market { get; set; }

    /// <summary>Kèo chấp: mức chấp của đội nhà (âm = đội nhà chấp). Tài xỉu: tổng bàn.</summary>
    public decimal Line { get; set; }

    /// <summary>Odds (decimal) cửa đội nhà / cửa tài.</summary>
    public decimal PriceA { get; set; }

    /// <summary>Odds (decimal) cửa đội khách / cửa xỉu.</summary>
    public decimal PriceB { get; set; }

    public int BookmakerId { get; set; }

    public DateTime? BookmakerUpdatedAtUtc { get; set; }

    public DateTime FetchedAtUtc { get; set; }

    public OddsSource Source { get; set; }

    public bool SameQuote(decimal line, decimal priceA, decimal priceB) =>
        Line == line && PriceA == priceA && PriceB == priceB;
}
