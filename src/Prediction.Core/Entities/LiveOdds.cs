namespace Prediction.Core.Entities;

/// <summary>Kèo chấp châu Á / tài xỉu bàn trong trận (từ odds/live) — chỉ ghi khi mức hoặc odds đổi. Chỉ để xem, không dùng để chấm.</summary>
public class LiveOddsSnapshot
{
    public long Id { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    /// <summary>AsianHandicap hoặc OverUnder.</summary>
    public Market Market { get; set; }

    /// <summary>Kèo hiệp 1 (true) hay cả trận (false).</summary>
    public bool FirstHalf { get; set; }

    /// <summary>
    /// Kèo chấp: mức chấp của đội nhà (âm = đội nhà chấp) tính trên tỉ số CẢ TRẬN như API-Football báo
    /// (vd đội nhà chấp 1.25 trước trận, đang thua 0-1 thì ghi +0.25). Tài xỉu: tổng bàn cả trận/hiệp (gồm bàn đã ghi).
    /// </summary>
    public decimal Line { get; set; }

    /// <summary>Odds cửa đội nhà / cửa tài.</summary>
    public decimal PriceA { get; set; }

    /// <summary>Odds cửa đội khách / cửa xỉu.</summary>
    public decimal PriceB { get; set; }

    /// <summary>Phút thi đấu lúc lấy kèo.</summary>
    public int? Minute { get; set; }

    /// <summary>Tỉ số lúc lấy kèo.</summary>
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }

    public DateTime FetchedAtUtc { get; set; }

    /// <summary>
    /// Kèo chấp quy về kiểu châu Á — tính từ tỉ số lúc báo kèo (bàn đã ghi không tính): mức cả trận + (bàn nhà − bàn khách).
    /// Tài xỉu giữ nguyên.
    /// </summary>
    public decimal FromScoreLine => Market == Market.AsianHandicap ? Line + (HomeGoals - AwayGoals) : Line;

    public bool SameQuote(decimal line, decimal priceA, decimal priceB) =>
        Line == line && PriceA == priceA && PriceB == priceB;
}
