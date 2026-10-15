using System.Globalization;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prediction.Core.Engine;
using Prediction.Core.Engine.Analysis;
using Prediction.Core.Entities;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>
/// AI (Claude + tra cứu web) điền thông tin bên lề của trận: lịch sử thù hận, động lực, tin lề, đặc điểm sân, thời tiết,
/// và kèo đóng của các trận cũ (đối đầu, 5 trận gần nhất) mà API không còn giữ.
/// </summary>
public sealed class SideInfoService(
    IOptions<AiReportOptions> options,
    IDbContextFactory<AppDbContext> dbFactory,
    MatchContextService contexts,
    TimeDisplay time,
    TimeProvider clock,
    ILogger<SideInfoService> logger)
{
    private const string Instructions = """
        Bạn là trợ lý tra cứu bóng đá cho website soi kèo cá nhân. Nhiệm vụ: dùng công cụ tìm kiếm web để điền THÔNG TIN BÊN LỀ
        của một trận đấu, ngắn gọn, bằng tiếng Việt, chỉ ghi điều tra cứu được (không bịa). Không cần phân tích kèo.

        Trả lời DUY NHẤT một khối ```json theo đúng cấu trúc:
        {
          "rivalry": "lịch sử thù hận / derby / đối đầu đáng chú ý giữa hai đội (2–4 câu), hoặc null",
          "motivation": "động lực của mỗi đội ở trận này (vị trí, mục tiêu, lịch thi đấu trước/sau), hoặc null",
          "news": "tin bên lề: chấn thương/treo giò quan trọng, xoay tua, nội bộ, phát biểu HLV (gạch đầu dòng bằng '- '), hoặc null",
          "venue_notes": "đặc điểm sân: mặt cỏ tự nhiên/nhân tạo, kích thước, khí hậu, độ cao nếu đáng kể, hoặc null",
          "weather": "dự báo thời tiết giờ bóng lăn nếu tìm được, hoặc null",
          "altitude_m": số mét so với mực nước biển của sân hoặc null,
          "surface": "grass" | "artificial turf" | "hybrid" | null,
          "lines": [ { "fixture_id": số, "ah_home_line": mức chấp của ĐỘI NHÀ trận đó (âm = đội nhà chấp, bội số 0.25) hoặc null,
                       "ou_line": mốc tài xỉu hoặc null, "source": "đường dẫn" } ],
          "sources": ["đường dẫn web đã dùng"]
        }

        Với "lines": chỉ điền kèo châu Á đóng (closing) của các trận cũ được liệt kê, khi tìm thấy nguồn đáng tin; không thấy thì bỏ qua trận đó.
        """;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public bool AutoEnabled => IsConfigured && options.Value.SideInfoAuto;

    public string Model => options.Value.SideInfoModel;

    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    /// <summary>Tự điền cho các trận nhóm A/B trong 24 giờ tới (trong giới hạn chi phí/ngày).</summary>
    public async Task<string?> RunAutoAsync(CancellationToken ct)
    {
        if (!AutoEnabled)
        {
            return null;
        }

        var o = options.Value;
        var now = UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var dayStart = now.Date;
        var spent = await db.MatchSideInfos.Where(s => s.IsAuto && s.CreatedAtUtc >= dayStart).SumAsync(s => (decimal?)s.CostUsd, ct) ?? 0;
        if (spent >= o.SideInfoDailyBudgetUsd)
        {
            return null;
        }

        var ids = await db.AnalysisPicks.AsNoTracking()
            .Where(p => p.Group != AnalysisGroup.C && p.Fixture!.KickoffUtc > now && p.Fixture.KickoffUtc <= now.AddHours(24)
                && p.Fixture.League!.IsTracked && !p.Fixture.League.IsDemo
                && !db.MatchSideInfos.Any(s => s.FixtureId == p.FixtureId))
            .OrderBy(p => p.Fixture!.KickoffUtc)
            .Select(p => p.FixtureId)
            .Distinct()
            .Take(o.SideInfoMaxPerRun)
            .ToListAsync(ct);

        var done = 0;
        var cost = 0m;
        foreach (var id in ids)
        {
            if (spent + cost >= o.SideInfoDailyBudgetUsd)
            {
                break;
            }

            try
            {
                var info = await GenerateAsync(id, true, ct);
                cost += info.CostUsd;
                done++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "AI điền thông tin bên lề trận {Fixture} lỗi", id);
            }
        }

        return done == 0 ? null : $"AI điền thông tin bên lề: {done} trận nhóm A/B, ~${cost:0.00} (hôm nay ~${spent + cost:0.00}/{o.SideInfoDailyBudgetUsd:0.##}).";
    }

    public async Task<MatchSideInfo> GenerateAsync(int fixtureId, bool isAuto, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Chưa cấu hình Anthropic:ApiKey.");
        }

        var view = await contexts.ForFixtureAsync(fixtureId, ct) ?? throw new InvalidOperationException("Không tìm thấy trận.");
        var o = options.Value;
        var client = string.IsNullOrWhiteSpace(o.BaseUrl)
            ? new AnthropicClient { ApiKey = o.ApiKey, Timeout = TimeSpan.FromMinutes(o.TimeoutMinutes) }
            : new AnthropicClient { ApiKey = o.ApiKey, Timeout = TimeSpan.FromMinutes(o.TimeoutMinutes), BaseUrl = o.BaseUrl };

        var missing = view.HeadToHead.Concat(view.HomeRecent).Concat(view.AwayRecent)
            .Where(r => r.Origin is LineOrigin.None or LineOrigin.Ai)
            .DistinctBy(r => r.FixtureId)
            .ToList();

        var response = await client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = o.SideInfoModel,
            MaxTokens = 8000,
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Default(),
            OutputConfig = new BetaOutputConfig { Effort = ParseEffort(o.SideInfoEffort) },
            System = Instructions,
            Tools = [new BetaWebSearchTool20260209 { MaxUses = o.SideInfoWebSearchMaxUses }],
            Messages = [new() { Role = Role.User, Content = Prompt(view, missing) }],
        }, ct);

        var text = string.Join("\n", response.Content.Select(b => b.TryPickText(out var t) ? t.Text : null).Where(t => t is not null));
        var stop = response.StopReason?.Raw();
        if (stop == "refusal")
        {
            throw new InvalidOperationException("Claude từ chối yêu cầu này.");
        }

        var usage = response.Usage;
        var searches = usage.ServerToolUse?.WebSearchRequests ?? 0;
        var (inPrice, outPrice) = Prices(response.Model.Raw());
        var cost = Math.Round((usage.InputTokens * inPrice + usage.OutputTokens * outPrice
            + (usage.CacheCreationInputTokens ?? 0) * inPrice * 1.25m + (usage.CacheReadInputTokens ?? 0) * inPrice * 0.1m) / 1_000_000m + searches * 0.01m, 4);

        var parsed = Parse(text) ?? throw new InvalidOperationException("AI không trả về JSON hợp lệ.");
        var now = UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var info = await db.MatchSideInfos.FindAsync([fixtureId], ct);
        if (info is null)
        {
            info = new MatchSideInfo { FixtureId = fixtureId };
            db.MatchSideInfos.Add(info);
        }

        info.Rivalry = Str(parsed, "rivalry", 2000);
        info.Motivation = Str(parsed, "motivation", 2000);
        info.News = Str(parsed, "news", 3000);
        info.VenueNotes = Str(parsed, "venue_notes", 1000);
        info.Weather = Str(parsed, "weather", 500);
        info.AltitudeM = parsed.TryGetProperty("altitude_m", out var alt) && alt.ValueKind == JsonValueKind.Number && alt.TryGetDouble(out var a) && a is > -500 and < 6000
            ? (int)Math.Round(a)
            : null;
        info.Surface = Str(parsed, "surface", 50);
        info.Sources = parsed.TryGetProperty("sources", out var src) && src.ValueKind == JsonValueKind.Array
            ? Truncate(string.Join("\n", src.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString())), 3000)
            : null;
        info.Model = response.Model.Raw();
        info.CostUsd = cost;
        info.IsAuto = isAuto;
        info.CreatedAtUtc = now;

        // Kèo trận cũ: chỉ nhận cho các trận đã hỏi, không ghi đè kèo nhập tay.
        var asked = missing.Select(m => m.FixtureId).ToHashSet();
        if (parsed.TryGetProperty("lines", out var lines) && lines.ValueKind == JsonValueKind.Array)
        {
            var existing = await db.ClosingLines.Where(c => asked.Contains(c.FixtureId)).ToDictionaryAsync(c => c.FixtureId, ct);
            foreach (var l in lines.EnumerateArray())
            {
                if (!l.TryGetProperty("fixture_id", out var idEl) || !idEl.TryGetInt32(out var id) || !asked.Contains(id))
                {
                    continue;
                }

                var ah = Line(l, "ah_home_line", -4m, 4m);
                var ou = Line(l, "ou_line", 0.5m, 7m);
                if (ah is null && ou is null || existing.GetValueOrDefault(id) is { Source: LineSource.Manual })
                {
                    continue;
                }

                if (!existing.TryGetValue(id, out var row))
                {
                    row = new ClosingLine { FixtureId = id };
                    db.ClosingLines.Add(row);
                    existing[id] = row;
                }

                row.HandicapLine = ah;
                row.TotalLine = ou;
                row.Source = LineSource.Ai;
                row.Note = Str(l, "source", 500);
                row.UpdatedAtUtc = now;
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("AI điền thông tin bên lề trận {Fixture}: {Searches} lượt web, ~${Cost}", fixtureId, searches, cost);
        return info;
    }

    private string Prompt(MatchContextView v, IReadOnlyList<HistoryLine> missing)
    {
        var f = v.Fixture;
        var sb = new StringBuilder();
        sb.AppendLine($"Trận: {v.Home} (nhà) – {v.Away} (khách), giờ bóng lăn {time.Full(f.KickoffUtc)} (giờ Việt Nam), vòng {f.Round}.");
        if (v.Venue is { } venue)
        {
            sb.AppendLine($"Sân: {venue.Name}, {venue.City}, {venue.Country}; mặt sân theo API: {venue.Surface ?? "chưa rõ"}; độ cao: {(venue.ElevationM is { } e ? $"{e} m" : "chưa rõ")}.");
        }
        else if (f.VenueName is not null)
        {
            sb.AppendLine($"Sân: {f.VenueName}.");
        }

        if (v.Weather is not null)
        {
            sb.AppendLine("Website đã có dự báo thời tiết; trường \"weather\" có thể để null.");
        }

        if (missing.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Các trận cũ cần kèo châu Á đóng (fixture_id | ngày | đội nhà – đội khách | tỉ số):");
            foreach (var m in missing.OrderByDescending(m => m.KickoffUtc))
            {
                sb.AppendLine($"- {m.FixtureId} | {m.KickoffUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} | {m.HomeName} – {m.AwayName} | {(m.HasScore ? $"{m.HomeGoals}-{m.AwayGoals}" : "?")}");
            }
        }

        return sb.ToString();
    }

    /// <summary>Lấy khối JSON trong câu trả lời (```json ... ``` hoặc từ '{' đầu tới '}' cuối).</summary>
    public static JsonElement? Parse(string text)
    {
        var start = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        string candidate;
        if (start >= 0)
        {
            var body = text[(start + 7)..];
            var end = body.IndexOf("```", StringComparison.Ordinal);
            candidate = end >= 0 ? body[..end] : body;
        }
        else
        {
            var first = text.IndexOf('{');
            var last = text.LastIndexOf('}');
            if (first < 0 || last <= first)
            {
                return null;
            }

            candidate = text[first..(last + 1)];
        }

        try
        {
            using var doc = JsonDocument.Parse(candidate, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Str(JsonElement e, string name, int max) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? Truncate(v.GetString()!.Trim(), max)
            : null;

    private static decimal? Line(JsonElement e, string name, decimal min, decimal max)
    {
        if (!e.TryGetProperty(name, out var v))
        {
            return null;
        }

        decimal? value = v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetDecimal(out var d) => d,
            JsonValueKind.String when decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
        return value is { } x && x >= min && x <= max && x * 4 == decimal.Truncate(x * 4) ? x : null;
    }

    private static string? Truncate(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..max];

    private static Effort ParseEffort(string value) => value.Trim().ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "high" => Effort.High,
        "max" => Effort.Max,
        _ => Effort.Medium,
    };

    /// <summary>Giá USD / triệu token (vào, ra) theo dòng model.</summary>
    public static (decimal Input, decimal Output) Prices(string model) => model switch
    {
        var m when m.Contains("opus", StringComparison.OrdinalIgnoreCase) => (4m, 20m),
        var m when m.Contains("haiku", StringComparison.OrdinalIgnoreCase) => (1m, 5m),
        var m when m.Contains("fable", StringComparison.OrdinalIgnoreCase) || m.Contains("mythos", StringComparison.OrdinalIgnoreCase) => (10m, 50m),
        _ => (2m, 10m),
    };
}
