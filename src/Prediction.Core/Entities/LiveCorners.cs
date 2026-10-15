namespace Prediction.Core.Entities;

/// <summary>Hiệp của khuyến nghị góc live: hiệp 1 (chấm theo góc hiệp 1) hoặc cả trận (vào từ hiệp 2).</summary>
public enum CornerWindow
{
    FirstHalf = 1,
    SecondHalf = 2,
}

/// <summary>Thống kê trực tiếp tại một phút (lưu mỗi lần đồng bộ trực tiếp) để tính nhịp độ gần nhất.</summary>
public class LiveStatSnapshot
{
    public long Id { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public int Minute { get; set; }

    public string Status { get; set; } = "";

    public DateTime CapturedAtUtc { get; set; }

    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public int HomeCorners { get; set; }
    public int AwayCorners { get; set; }
    public int? HomeShots { get; set; }
    public int? AwayShots { get; set; }
    public int? HomeOnTarget { get; set; }
    public int? AwayOnTarget { get; set; }
    public int? HomeBlocked { get; set; }
    public int? AwayBlocked { get; set; }
    public int HomeRed { get; set; }
    public int AwayRed { get; set; }

    public int Corners => HomeCorners + AwayCorners;

    public int Shots => (HomeShots ?? 0) + (AwayShots ?? 0);
}

/// <summary>Kèo tài xỉu góc live (hiệp 1 hoặc cả trận) — chỉ ghi khi đổi.</summary>
public class LiveCornerOdds
{
    public long Id { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public CornerWindow Window { get; set; }

    public decimal Line { get; set; }
    public decimal OverOdds { get; set; }
    public decimal UnderOdds { get; set; }

    /// <summary>Phút thi đấu lúc lấy kèo.</summary>
    public int? Minute { get; set; }

    public DateTime FetchedAtUtc { get; set; }
}

/// <summary>Khuyến nghị góc live được chốt trong cửa sổ vàng (H1 phút 25–30, H2 phút 75–80), để chấm thành tích.</summary>
public class LiveCornerPick
{
    public long Id { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public CornerWindow Window { get; set; }

    public int Minute { get; set; }

    public PickSide Side { get; set; }

    public decimal Line { get; set; }

    public decimal Odds { get; set; }

    public int CornersAtPick { get; set; }

    public decimal ExpectedTotal { get; set; }

    public decimal Edge { get; set; }

    public decimal WinProbability { get; set; }

    public decimal Confidence { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Số góc chấm (hết hiệp 1 hoặc hết trận).</summary>
    public int? FinalCorners { get; set; }

    public BetOutcome Outcome { get; set; }

    public decimal? Profit { get; set; }

    public DateTime? SettledAtUtc { get; set; }
}
