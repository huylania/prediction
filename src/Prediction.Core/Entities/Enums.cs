namespace Prediction.Core.Entities;

/// <summary>Loại kèo.</summary>
public enum Market
{
    AsianHandicap = 1,
    OverUnder = 2,
    CornersOverUnder = 3,
    CornersHandicap = 4,
    CardsOverUnder = 5,
}

/// <summary>Nguồn của một bản ghi kèo.</summary>
public enum OddsSource
{
    Api = 1,
    Manual = 2,
    Demo = 3,
}

/// <summary>Lựa chọn của một quy tắc, nhìn từ góc độ "đội A" (đội được xét).</summary>
public enum RuleSelection
{
    Subject = 1,
    Opponent = 2,
    Over = 3,
    Under = 4,
}

/// <summary>Cửa cụ thể của một lựa chọn trong một trận.</summary>
public enum PickSide
{
    Home = 1,
    Away = 2,
    Over = 3,
    Under = 4,
}

public enum VenueFilter
{
    Any = 0,
    Home = 1,
    Away = 2,
}

public enum AhMovementFilter
{
    Any = 0,
    TowardSubject = 1,
    TowardOpponent = 2,
    Unchanged = 3,
}

public enum OuMovementFilter
{
    Any = 0,
    TowardOver = 1,
    TowardUnder = 2,
    Unchanged = 3,
}

public enum BetOutcome
{
    Pending = 0,
    Win = 1,
    HalfWin = 2,
    Push = 3,
    HalfLoss = 4,
    Loss = 5,
    Void = 6,
}

/// <summary>Mẫu kèo người chơi hay thắng (từ phân tích phiếu cược): chấm riêng bằng bộ chỉ số giờ + điểm.</summary>
public enum LinePattern
{
    /// <summary>Tài đúng 3.0 bàn (hoàn tiền khi có đúng 3 bàn).</summary>
    Over3 = 1,

    /// <summary>Cửa trên chấp đúng 0.75 (thắng 1 bàn: ăn nửa).</summary>
    Favourite075 = 2,

    /// <summary>Cửa trên chấp đúng 1.0 (thắng 1 bàn: hoàn tiền).</summary>
    Favourite1 = 3,
}

public static class PickSideExtensions
{
    public static Market Market(this PickSide side) =>
        side is PickSide.Home or PickSide.Away ? Entities.Market.AsianHandicap : Entities.Market.OverUnder;

    public static PickSide Opposite(this PickSide side) => side switch
    {
        PickSide.Home => PickSide.Away,
        PickSide.Away => PickSide.Home,
        PickSide.Over => PickSide.Under,
        _ => PickSide.Over,
    };
}
