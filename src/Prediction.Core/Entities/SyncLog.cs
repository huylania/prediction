namespace Prediction.Core.Entities;

/// <summary>Nhật ký một lần đồng bộ dữ liệu.</summary>
public class SyncLog
{
    public long Id { get; set; }

    public string Job { get; set; } = "";

    public DateTime StartedAtUtc { get; set; }

    public DateTime FinishedAtUtc { get; set; }

    public bool Ok { get; set; }

    public int Requests { get; set; }

    public string? Message { get; set; }
}
