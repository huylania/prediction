namespace Prediction.Core.Entities;

/// <summary>Dự đoán/tư vấn của API-Football (endpoint /predictions), chỉ lưu bản lấy trước giờ bóng lăn.</summary>
public class ApiAdvice
{
    public long Id { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public DateTime FetchedAtUtc { get; set; }

    public int? WinnerTeamId { get; set; }

    public string? WinnerComment { get; set; }

    public bool? WinOrDraw { get; set; }

    /// <summary>Ví dụ "-2.5" (xỉu 2.5) hoặc "+1.5" (tài 1.5).</summary>
    public string? UnderOver { get; set; }

    public string? Advice { get; set; }

    public int? PercentHome { get; set; }
    public int? PercentDraw { get; set; }
    public int? PercentAway { get; set; }

    public int? FormHome { get; set; }
    public int? FormAway { get; set; }
    public int? AttackHome { get; set; }
    public int? AttackAway { get; set; }
    public int? DefenceHome { get; set; }
    public int? DefenceAway { get; set; }
    public int? H2HHome { get; set; }
    public int? H2HAway { get; set; }
    public int? TotalHome { get; set; }
    public int? TotalAway { get; set; }

    /// <summary>Hướng tài/xỉu API dự đoán: Over, Under hoặc null.</summary>
    public PickSide? UnderOverSide
    {
        get
        {
            var value = UnderOver?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return value[0] switch
            {
                '-' => PickSide.Under,
                '+' => PickSide.Over,
                _ => null,
            };
        }
    }
}
