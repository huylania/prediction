namespace Prediction.Core.Entities;

public enum TeamChangeKind
{
    NewManager = 1,
    KeySigning = 2,
}

public enum TeamChangeSource
{
    Api = 1,
    Manual = 2,
}

/// <summary>Thay đổi nhân sự lớn của một đội: HLV mới hoặc tân binh trụ cột (quy tắc chờ ổn định 2–3 trận).</summary>
public class TeamChange
{
    public long Id { get; set; }

    public int TeamId { get; set; }
    public Team? Team { get; set; }

    public TeamChangeKind Kind { get; set; }

    public string PersonName { get; set; } = "";

    /// <summary>Ngày bắt đầu (HLV nhận việc / tân binh đá chính lần đầu).</summary>
    public DateTime StartDate { get; set; }

    public TeamChangeSource Source { get; set; }

    public string? Notes { get; set; }

    /// <summary>Bỏ qua thay đổi này (vd thông tin API sai).</summary>
    public bool IsIgnored { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
