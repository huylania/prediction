namespace Prediction.Core.Entities;

/// <summary>Báo cáo phân tích do Claude viết theo file kỹ năng, lưu lại để không phải trả phí lần nữa.</summary>
public class AiReport
{
    public long Id { get; set; }

    public int FixtureId { get; set; }
    public Fixture? Fixture { get; set; }

    public string Model { get; set; } = "";

    /// <summary>Nội dung báo cáo (markdown).</summary>
    public string Markdown { get; set; } = "";

    /// <summary>"end_turn", "max_tokens", "refusal", "pause_turn"...</summary>
    public string? StopReason { get; set; }

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheReadTokens { get; set; }
    public int CacheWriteTokens { get; set; }
    public int WebSearches { get; set; }

    /// <summary>Chi phí ước tính (USD).</summary>
    public decimal CostUsd { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
