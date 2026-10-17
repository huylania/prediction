using System.Globalization;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prediction.Core.Engine.Analysis;
using Prediction.Core.Engine.LiveCorners;
using Prediction.Core.Entities;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>Form "tính tay / từ ảnh AiScore" của mục Góc Live (sửa được trước khi tính).</summary>
public sealed class LiveCornerForm
{
    /// <summary>Trận website đang theo dõi khi form được điền từ thẻ trận (null nếu nhập tay / đọc ảnh).</summary>
    public int? FixtureId { get; set; }

    public string? Home { get; set; }
    public string? Away { get; set; }
    public CornerWindow Window { get; set; } = CornerWindow.FirstHalf;
    public int Minute { get; set; } = 27;
    public int HomeCorners { get; set; }
    public int AwayCorners { get; set; }

    /// <summary>Tổng góc hết hiệp 1 (C_H1) — cửa sổ H2 dùng để tách nhịp góc riêng hiệp 2.</summary>
    public int? FirstHalfCorners { get; set; }

    public int? Shots { get; set; }
    public int? ShotsOnTarget { get; set; }
    public int? BlockedShots { get; set; }
    public int? DangerousAttacks { get; set; }
    public int? HomePossession { get; set; }
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public int HomeRed { get; set; }
    public int AwayRed { get; set; }
    public decimal? Line { get; set; }
    public decimal? OverOdds { get; set; }
    public decimal? UnderOdds { get; set; }
    public decimal? OpenLine { get; set; }
    public double? StoppageMinutes { get; set; }
    public Motivation Motivation { get; set; }
    public bool DrawSuitsBoth { get; set; }
    public PickSide? Favourite { get; set; }

    /// <summary>Độ sâu kèo chấp trước trận của cửa trên (vd 0.75).</summary>
    public decimal? PreMatchHandicap { get; set; }

    /// <summary>Ghi chú Claude đọc được từ ảnh (xu hướng tấn công, thẻ vàng hậu vệ...).</summary>
    public string? Notes { get; set; }

    public LiveCornerInput ToInput() => new()
    {
        Window = Window,
        Minute = Math.Clamp(Minute, 1, 130),
        HomeCorners = Math.Max(0, HomeCorners),
        AwayCorners = Math.Max(0, AwayCorners),
        FirstHalfCorners = Window == CornerWindow.SecondHalf && FirstHalfCorners is { } h1 ? Math.Clamp(h1, 0, Math.Max(0, HomeCorners) + Math.Max(0, AwayCorners)) : null,
        Shots = Shots,
        ShotsOnTarget = ShotsOnTarget,
        BlockedShots = BlockedShots,
        DangerousAttacks = DangerousAttacks,
        HomePossession = HomePossession,
        HomeGoals = Math.Max(0, HomeGoals),
        AwayGoals = Math.Max(0, AwayGoals),
        HomeRed = Math.Max(0, HomeRed),
        AwayRed = Math.Max(0, AwayRed),
        Line = Line,
        OverOdds = OverOdds,
        UnderOdds = UnderOdds,
        OpenLine = OpenLine,
        StoppageMinutes = StoppageMinutes,
        Motivation = Motivation,
        DrawSuitsBoth = DrawSuitsBoth,
        Favourite = Favourite,
        PreMatchHandicap = PreMatchHandicap is { } h ? Math.Abs(h) : null,
    };

    /// <summary>Điền form từ một trận website đang theo dõi (để sửa tay hoặc bổ sung tấn công nguy hiểm).</summary>
    public static LiveCornerForm From(LiveCornerMatch m) => new()
    {
        FixtureId = m.Fixture.Id,
        Home = m.Home,
        Away = m.Away,
        Window = m.Input.Window,
        Minute = m.Input.Minute,
        HomeCorners = m.Input.HomeCorners,
        AwayCorners = m.Input.AwayCorners,
        FirstHalfCorners = m.Input.FirstHalfCorners,
        Shots = m.Input.Shots,
        ShotsOnTarget = m.Input.ShotsOnTarget,
        BlockedShots = m.Input.BlockedShots,
        DangerousAttacks = m.Input.DangerousAttacks,
        HomePossession = m.Input.HomePossession,
        HomeGoals = m.Input.HomeGoals,
        AwayGoals = m.Input.AwayGoals,
        HomeRed = m.Input.HomeRed,
        AwayRed = m.Input.AwayRed,
        Line = m.Input.Line,
        OverOdds = m.Input.OverOdds,
        UnderOdds = m.Input.UnderOdds,
        OpenLine = m.Input.OpenLine,
        Motivation = m.Input.Motivation,
        DrawSuitsBoth = m.Input.DrawSuitsBoth,
        Favourite = m.Input.Favourite,
        PreMatchHandicap = m.Input.PreMatchHandicap,
    };
}

/// <summary>Ảnh tải lên (AiScore) để Claude đọc.</summary>
public sealed record CornerImage(byte[] Data, string ContentType);

/// <summary>Kết quả một lượt gọi Claude của mục Góc Live.</summary>
public sealed record CornerAiCost(string Model, long InputTokens, long OutputTokens, decimal CostUsd);

/// <summary>
/// AI của mục Góc Live — dùng RIÊNG file kỹ năng "phan-tich-goc" (không dùng chung với báo cáo phân tích chuyên sâu):
/// đọc ảnh chụp AiScore thành số liệu cho form, và "Claude nhận định" từ số liệu + kết quả công thức của website.
/// </summary>
public sealed class CornerAiService(
    IOptions<AiReportOptions> options,
    IWebHostEnvironment env,
    AiUsageService usage,
    IDbContextFactory<AppDbContext> dbFactory,
    TimeProvider clock,
    ILogger<CornerAiService> logger)
{
    private const string VisionInstructions = """
        Bạn đọc ảnh chụp màn hình trận đang đá trên AiScore (hoặc ứng dụng tỉ số tương tự) cho mục "Góc Live" của website soi kèo cá nhân.
        Trích đúng các nhóm chỉ số ở mục 2 của kỹ năng: phút, số góc mỗi đội (và số góc hiệp 1 nếu ảnh có, khi đang ở hiệp 2),
        mốc kèo góc (mở trận và live, kèm odds Tài/Xỉu nếu thấy), cú sút (tổng, trúng đích, bị chặn), tấn công nguy hiểm, thẻ đỏ,
        tỉ số, kiểm soát bóng. Chỉ ghi số NHÌN THẤY trên ảnh, không đoán;
        không thấy thì để null. Nếu có nhiều ảnh thì gộp lại (ảnh sau mới hơn).

        Kèo góc: nếu ảnh là kèo góc hiệp 1 thì "window" = "H1", kèo góc cả trận thì "H2"; nếu không rõ thì suy từ phút
        (phút ≤ 45 hoặc đang hiệp 1 → "H1", ngược lại "H2"). Mốc góc là tổng hai đội (vd 9.5, 10, 10.25).

        Trả lời DUY NHẤT một khối ```json:
        {
          "home": "tên đội nhà" | null, "away": "tên đội khách" | null,
          "window": "H1" | "H2", "minute": số phút,
          "home_corners": số, "away_corners": số,
          "home_corners_h1": số | null, "away_corners_h1": số | null,
          "home_shots": số | null, "away_shots": số | null,
          "home_on_target": số | null, "away_on_target": số | null,
          "home_blocked": số | null, "away_blocked": số | null,
          "home_dangerous_attacks": số | null, "away_dangerous_attacks": số | null,
          "home_possession": số % | null,
          "home_goals": số, "away_goals": số, "home_red": số, "away_red": số,
          "line_open": mốc | null, "line_live": mốc | null, "over_odds": odds thập phân | null, "under_odds": odds thập phân | null,
          "notes": "điểm đáng chú ý khác trên ảnh (xu hướng tấn công biên/trung lộ, thẻ vàng hậu vệ, giải đấu...) hoặc null"
        }
        Odds kiểu Hồng Kông / Malaysia (vd 0.95, -0.90) thì đổi sang thập phân (0.95 → 1.95; -0.90 → 1 + 1/0.90).
        """;

    private const string CommentaryInstructions = """
        Bạn đang chạy trong mục "Góc Live" của website soi kèo cá nhân (Blazor + API-Football). Tin nhắn gồm số liệu live của MỘT trận
        tại phút T (tự động từ API-Football hoặc đọc từ ảnh AiScore và người dùng đã kiểm lại) và kết quả công thức của website
        theo đúng mô hình v2.0 mục 3/4 của kỹ năng (R có hồi quy / tách nhịp H2, hệ số α hoặc β đã kẹp biên độ, Exp, D_line, Δedge,
        xác suất Poisson và tỉ lệ thắng không tính hòa, trạng thái từng bộ lọc sắt, điểm tin cậy theo bậc, EV theo odds live).
        API-Football không có "tấn công nguy hiểm": khi thiếu, website dùng (sút + góc)/phút và nhịp 10–15 phút gần nhất làm chỉ số thay thế.

        Trả lời bằng tiếng Việt, Markdown, đúng CẤU TRÚC PHẢN HỒI CHUẨN MỰC ở mục 5, ngắn gọn. Giữ nguyên các con số của website;
        nếu bạn đánh giá khác hệ số nào (vd động lực, thế trận) thì nói rõ lý do và con số bạn đề xuất. Không bịa số liệu thiếu.
        """;

    private string? _skill;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public string Model => options.Value.CornerModel;

    public const long MaxImageBytes = 5 * 1024 * 1024;

    /// <summary>Claude đọc 1–2 ảnh AiScore → form đã điền (người dùng kiểm/sửa trước khi tính).</summary>
    public async Task<(LiveCornerForm Form, CornerAiCost Cost)> ReadScreenshotsAsync(IReadOnlyList<CornerImage> images, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Chưa cấu hình Anthropic:ApiKey.");
        }

        if (images.Count == 0)
        {
            throw new InvalidOperationException("Chưa chọn ảnh.");
        }

        var o = options.Value;
        var content = new List<BetaContentBlockParam>();
        foreach (var image in images.Take(3))
        {
            content.Add(new BetaImageBlockParam
            {
                Source = new BetaBase64ImageSource { Data = Convert.ToBase64String(image.Data), MediaType = MediaTypeOf(image.ContentType) },
            });
        }

        content.Add(new BetaTextBlockParam { Text = "Đọc các ảnh trên và trả về JSON theo đúng cấu trúc." });

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var subject = $"Đọc {images.Count} ảnh AiScore";
        BetaMessage response;
        try
        {
            response = await CreateAsync();
        }
        catch (Exception ex)
        {
            await usage.LogAsync(new AiUsageLog
            {
                Feature = AiFeature.CornerVision, Model = o.CornerModel, Subject = subject, DurationMs = (int)watch.ElapsedMilliseconds,
                Ok = false, Error = ex is OperationCanceledException ? "Đã dừng / quá thời gian" : ex.Message,
            });
            throw;
        }

        async Task<BetaMessage> CreateAsync() => await Client(o).Beta.Messages.Create(new MessageCreateParams
        {
            Model = o.CornerModel,
            MaxTokens = 4000,
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Default(),
            OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
            System = new List<BetaTextBlockParam>
            {
                new() { Text = await SkillAsync(ct) },
                new() { Text = VisionInstructions, CacheControl = new BetaCacheControlEphemeral { Ttl = Ttl.Ttl1h } },
            },
            Messages = [new() { Role = Role.User, Content = content }],
        }, ct);

        var text = string.Join("\n", response.Content.Select(b => b.TryPickText(out var t) ? t.Text : null).Where(t => t is not null));
        var refused = response.StopReason?.Raw() == "refusal";
        var json = refused ? null : SideInfoService.Parse(text);
        var u = response.Usage;
        var model = response.Model.Raw();
        var log = new AiUsageLog
        {
            Feature = AiFeature.CornerVision, Model = model, Subject = subject,
            InputTokens = (int)u.InputTokens, OutputTokens = (int)u.OutputTokens,
            CacheReadTokens = (int)(u.CacheReadInputTokens ?? 0), CacheWriteTokens = (int)(u.CacheCreationInputTokens ?? 0),
            CostUsd = AiPricing.Cost(model, u.InputTokens, u.OutputTokens, u.CacheReadInputTokens ?? 0, u.CacheCreationInputTokens ?? 0, 0, cacheWrite1h: true),
            DurationMs = (int)watch.ElapsedMilliseconds, Ok = json is not null,
            Error = refused ? "Claude từ chối đọc ảnh" : json is null ? "Không trả về JSON hợp lệ" : null,
        };
        await usage.LogAsync(log);
        if (refused)
        {
            throw new InvalidOperationException("Claude đã từ chối đọc ảnh này.");
        }

        if (json is null)
        {
            throw new InvalidOperationException("Claude không trả về JSON hợp lệ — thử ảnh rõ hơn.");
        }

        var cost = new CornerAiCost(model, log.InputTokens + log.CacheReadTokens + log.CacheWriteTokens, log.OutputTokens, log.CostUsd);
        logger.LogInformation("Góc Live: đọc {Count} ảnh AiScore, {Input} token vào, {Output} ra, ~${Cost}", images.Count, cost.InputTokens, cost.OutputTokens, cost.CostUsd);
        var form = ParseForm(json.Value);
        var formInput = form.ToInput();
        var note = NoteFor(CornerAiKind.ReadImages, FormMatch(form), null, formInput, form.Minute > 0 ? LiveCornerAnalyzer.Analyze(formInput) : null);
        note.DataJson = json.Value.GetRawText();
        note.Markdown = form.Notes;
        note.Model = model;
        note.CostUsd = log.CostUsd;
        await SaveNoteAsync(note);
        return (form, cost);
    }

    /// <summary>Tên trận của form (đội đọc từ ảnh / nhập tay).</summary>
    public static string FormMatch(LiveCornerForm form) =>
        string.IsNullOrWhiteSpace(form.Home) && string.IsNullOrWhiteSpace(form.Away) ? "Trận nhập tay" : $"{form.Home ?? "?"} – {form.Away ?? "?"}";

    /// <summary>Bản ghi một lần gọi Claude ở Góc Live, chụp lại tình hình trận và kết luận công thức lúc đó.</summary>
    public static CornerAiNote NoteFor(CornerAiKind kind, string match, int? fixtureId, LiveCornerInput input, LiveCornerResult? result) => new()
    {
        Kind = kind,
        FixtureId = fixtureId,
        Match = match.Length > 200 ? match[..200] : match,
        Window = input.Window,
        Minute = input.Minute,
        HomeCorners = input.HomeCorners,
        AwayCorners = input.AwayCorners,
        HomeGoals = input.HomeGoals,
        AwayGoals = input.AwayGoals,
        Line = input.Line,
        OverOdds = input.OverOdds,
        UnderOdds = input.UnderOdds,
        ExpectedTotal = result is null ? null : Math.Round(result.ExpectedTotal, 2),
        Headline = result?.Headline is { Length: > 300 } h ? h[..300] : result?.Headline,
    };

    /// <summary>Lưu lần gọi Claude ở Góc Live. Lỗi ghi CSDL chỉ ghi log, không làm hỏng tính năng.</summary>
    private async Task SaveNoteAsync(CornerAiNote note)
    {
        try
        {
            note.CreatedAtUtc = clock.GetUtcNow().UtcDateTime;
            await using var db = await dbFactory.CreateDbContextAsync(CancellationToken.None);
            db.CornerAiNotes.Add(note);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không lưu được kết quả Claude ở Góc Live ({Match})", note.Match);
        }
    }

    /// <summary>
    /// Các lần gọi Claude ở Góc Live, mới nhất trước, kèm số góc cuối để so lại:
    /// H1 = góc hết hiệp 1, H2 = tổng góc cả trận (chỉ khi trận đã kết thúc); null nếu chưa biết hoặc không gắn trận.
    /// </summary>
    public async Task<List<(CornerAiNote Note, int? FinalCorners)>> HistoryAsync(int take, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var notes = await db.CornerAiNotes.AsNoTracking().OrderByDescending(n => n.CreatedAtUtc).Take(take).ToListAsync(ct);
        var ids = notes.Where(n => n.FixtureId is not null).Select(n => n.FixtureId!.Value).Distinct().ToList();
        var stats = await db.FixtureStats.AsNoTracking().Include(s => s.Fixture).Where(s => ids.Contains(s.FixtureId)).ToDictionaryAsync(s => s.FixtureId, ct);
        return notes.Select(n =>
        {
            int? final = null;
            if (n.FixtureId is { } id && stats.TryGetValue(id, out var st))
            {
                final = n.Window == CornerWindow.FirstHalf
                    ? st.FirstHalfHomeCorners + st.FirstHalfAwayCorners
                    : st.Fixture?.IsFinished == true ? st.TotalCorners : null;
            }

            return (n, final);
        }).ToList();
    }

    /// <summary>Nhận định mới nhất đã lưu của một trận ở một cửa sổ (H1/H2).</summary>
    public async Task<CornerAiNote?> LatestCommentaryAsync(int fixtureId, CornerWindow window, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CornerAiNotes.AsNoTracking()
            .Where(n => n.FixtureId == fixtureId && n.Window == window && n.Kind == CornerAiKind.Commentary)
            .OrderByDescending(n => n.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Chuyển JSON Claude đọc từ ảnh thành form.</summary>
    public static LiveCornerForm ParseForm(JsonElement e)
    {
        var minute = Int(e, "minute") ?? 0;
        var window = Str(e, "window")?.ToUpperInvariant() switch
        {
            "H1" or "1H" => CornerWindow.FirstHalf,
            "H2" or "2H" or "FT" => CornerWindow.SecondHalf,
            _ => minute is > 0 and <= 45 ? CornerWindow.FirstHalf : CornerWindow.SecondHalf,
        };

        int? Sum(string home, string away) => Int(e, home).HasValue || Int(e, away).HasValue ? (Int(e, home) ?? 0) + (Int(e, away) ?? 0) : null;

        return new LiveCornerForm
        {
            Home = Str(e, "home"),
            Away = Str(e, "away"),
            Window = window,
            Minute = minute,
            HomeCorners = Int(e, "home_corners") ?? 0,
            AwayCorners = Int(e, "away_corners") ?? 0,
            FirstHalfCorners = Sum("home_corners_h1", "away_corners_h1"),
            Shots = Sum("home_shots", "away_shots"),
            ShotsOnTarget = Sum("home_on_target", "away_on_target"),
            BlockedShots = Sum("home_blocked", "away_blocked"),
            DangerousAttacks = Sum("home_dangerous_attacks", "away_dangerous_attacks"),
            HomePossession = Int(e, "home_possession"),
            HomeGoals = Int(e, "home_goals") ?? 0,
            AwayGoals = Int(e, "away_goals") ?? 0,
            HomeRed = Int(e, "home_red") ?? 0,
            AwayRed = Int(e, "away_red") ?? 0,
            OpenLine = Line(e, "line_open"),
            Line = Line(e, "line_live"),
            OverOdds = Odds(e, "over_odds"),
            UnderOdds = Odds(e, "under_odds"),
            Notes = Str(e, "notes"),
        };
    }

    /// <summary>"Claude nhận định": gửi số liệu + kết quả công thức, nhận phần trả lời theo mục 5 dạng streaming.</summary>
    public async Task<(string Markdown, CornerAiCost Cost)> CommentaryAsync(
        string match, int? fixtureId, LiveCornerInput input, LiveCornerResult result, string? notes, Func<AiReportUpdate, Task> onUpdate, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Chưa cấu hình Anthropic:ApiKey.");
        }

        var o = options.Value;
        var parameters = new MessageCreateParams
        {
            Model = o.CornerModel,
            MaxTokens = o.CornerMaxTokens,
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Default(),
            OutputConfig = new BetaOutputConfig { Effort = ParseEffort(o.CornerEffort) },
            System = new List<BetaTextBlockParam>
            {
                new() { Text = await SkillAsync(ct) },
                new() { Text = CommentaryInstructions, CacheControl = new BetaCacheControlEphemeral { Ttl = Ttl.Ttl1h } },
            },
            Messages = [new() { Role = Role.User, Content = UserMessage(match, input, result, notes) }],
        };


        var text = new StringBuilder();
        string? stopReason = null;
        long inTokens = 0, output = 0, cacheRead = 0, cacheWrite = 0;
        var model = o.CornerModel;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        AiUsageLog Log(bool ok, string? error) => new()
        {
            Feature = AiFeature.CornerCommentary, Model = model, Subject = match,
            InputTokens = (int)inTokens, OutputTokens = (int)output, CacheReadTokens = (int)cacheRead, CacheWriteTokens = (int)cacheWrite,
            CostUsd = AiPricing.Cost(model, inTokens, output, cacheRead, cacheWrite, 0, cacheWrite1h: true),
            DurationMs = (int)watch.ElapsedMilliseconds, Ok = ok, Error = error,
        };

        CornerAiNote Note(string markdown, bool complete, decimal cost)
        {
            var note = NoteFor(CornerAiKind.Commentary, match, fixtureId, input, result);
            note.DataJson = DataJson(match, input, result, notes);
            note.Markdown = markdown;
            note.Model = model;
            note.CostUsd = cost;
            note.Complete = complete;
            return note;
        }

        try
        {
            await foreach (var ev in Client(o).Beta.Messages.CreateStreaming(parameters, ct))
            {
                if (ev.TryPickStart(out var start))
                {
                    model = start.Message.Model.Raw();
                    inTokens = start.Message.Usage.InputTokens;
                    cacheRead = start.Message.Usage.CacheReadInputTokens ?? 0;
                    cacheWrite = start.Message.Usage.CacheCreationInputTokens ?? 0;
                }
                else if (ev.TryPickContentBlockDelta(out var delta) && delta.Delta.TryPickText(out var chunk))
                {
                    text.Append(chunk.Text);
                    await onUpdate(new AiReportUpdate(chunk.Text, null));
                }
                else if (ev.TryPickDelta(out var messageDelta))
                {
                    stopReason = messageDelta.Delta.StopReason?.Raw();
                    output = messageDelta.Usage.OutputTokens;
                }
            }
        }
        catch (Exception ex)
        {
            var failed = Log(false, ex is OperationCanceledException ? "Đã dừng / quá thời gian" : ex.Message);
            await usage.LogAsync(failed);
            if (text.Length > 0)
            {
                // Phần đã nhận vẫn bị tính tiền: lưu thành bản dở dang.
                await SaveNoteAsync(Note(text.ToString().Trim() + $"\n\n> Bản dở dang ({failed.Error}).", false, failed.CostUsd));
            }

            throw;
        }

        var markdown = text.ToString().Trim();
        if (stopReason == "refusal")
        {
            markdown += "\n\n> Claude đã từ chối hoàn thành yêu cầu này. Hãy thử lại sau.";
        }
        else if (stopReason == "max_tokens")
        {
            markdown += "\n\n> Nhận định bị cắt do chạm giới hạn độ dài (Anthropic:CornerMaxTokens).";
        }

        var log = Log(stopReason is not ("refusal" or "max_tokens"), stopReason is "refusal" or "max_tokens" ? $"stop_reason = {stopReason}" : null);
        await usage.LogAsync(log);
        await SaveNoteAsync(Note(markdown, log.Ok, log.CostUsd));
        var cost = new CornerAiCost(model, inTokens + cacheRead + cacheWrite, output, log.CostUsd);
        logger.LogInformation("Góc Live: Claude nhận định {Match}, {Input} token vào, {Output} ra, ~${Cost}", match, cost.InputTokens, output, cost.CostUsd);
        return (markdown, cost);
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    /// <summary>Tin nhắn gửi Claude: số liệu live + bóc tách của website (JSON).</summary>
    public static string UserMessage(string match, LiveCornerInput i, LiveCornerResult r, string? notes) =>
        "Số liệu live và kết quả công thức của website (JSON):\n```json\n" + DataJson(match, i, r, notes) + "\n```\n"
        + "Hãy nhận định theo đúng cấu trúc mục 5 của kỹ năng.";

    /// <summary>Số liệu live + kết quả công thức gửi kèm "Claude nhận định" (JSON).</summary>
    public static string DataJson(string match, LiveCornerInput i, LiveCornerResult r, string? notes)
    {
        var data = new
        {
            tran = match,
            cua_so = i.Window == CornerWindow.FirstHalf ? "H1 (phút 25–30, kèo góc hiệp 1)" : "H2 (phút 75–80, kèo góc cả trận)",
            so_lieu_live = new
            {
                phut = i.Minute,
                ti_so = $"{i.HomeGoals}-{i.AwayGoals}",
                goc = new { doi_nha = i.HomeCorners, doi_khach = i.AwayCorners, tong = i.Corners, het_hiep_1 = i.FirstHalfCorners },
                sut = i.Shots,
                trung_dich = i.ShotsOnTarget,
                bi_chan = i.BlockedShots,
                tan_cong_nguy_hiem = i.DangerousAttacks,
                the_do = new { doi_nha = i.HomeRed, doi_khach = i.AwayRed },
                nhip_gan_nhat = i.RecentMinutes is { } rm ? new { so_phut = rm, sut = i.RecentShots, goc = i.RecentCorners, bi_chan = i.RecentBlocked } : null,
                keo_goc = new { mo_tran = i.OpenLine, live = i.Line, odds_tai = i.OverOdds, odds_xiu = i.UnderOdds },
                dong_luc = i.Motivation switch { Motivation.High => "cao (cần điểm sống còn / knock-out)", Motivation.Low => "thấp (trận thủ tục)", _ => "bình thường" },
                hoa_hai_ben_hai_long = i.DrawSuitsBoth ? true : (bool?)null,
                cua_tren_truoc_tran = i.Favourite switch { PickSide.Home => "đội nhà", PickSide.Away => "đội khách", _ => null },
                keo_chap_truoc_tran = i.PreMatchHandicap,
                kiem_soat_bong_doi_nha = i.HomePossession,
                ghi_chu = notes,
            },
            ket_qua_website = new
            {
                R_tho_C_tren_T = Math.Round(r.RawRate, 4),
                R_rieng_hiep_2 = r.SecondHalfRate is { } h2 ? Math.Round(h2, 4) : (double?)null,
                R_da_hieu_chinh = Math.Round(r.Rate, 4),
                he_so = r.Factors.Select(f => new { ten = f.Name, gia_tri = f.Value, ly_do = f.Reason }),
                tich_he_so_truoc_khi_kep = Math.Round(r.RawMultiplier, 3),
                tich_he_so = Math.Round(r.Multiplier, 3),
                delta_T = r.RemainingMinutes,
                goc_con_lai_du_kien = Math.Round(r.ExpectedRemaining, 2),
                exp_tong_goc = Math.Round(r.ExpectedTotal, 2),
                moc_live = r.Line,
                D_line = r.DLine is { } dl ? Math.Round(dl, 2) : (double?)null,
                delta_edge = r.Edge is { } edge ? Math.Round(edge, 2) : (double?)null,
                da_an_bai = r.Window == CornerWindow.SecondHalf ? r.Settled : (bool?)null,
                khuyen_nghi = r.Headline,
                xac_suat = r.Probability is { } p ? new
                {
                    cua = r.ProbabilitySide == PickSide.Over ? "Tài" : "Xỉu",
                    thang = Math.Round(p.WinAny, 3),
                    hoan = Math.Round(p.Push, 3),
                    thua = Math.Round(p.LossAny, 3),
                } : null,
                ty_le_thang_khong_tinh_hoa = r.WinRate is { } wr ? Math.Round(wr, 3) : (double?)null,
                bo_loc_sat = r.Filters.Select(f => new { ten = f.Name, dat = f.Passed, chi_tiet = f.Text }),
                ev = r.ExpectedValue is { } ev ? Math.Round(ev, 3) : (double?)null,
                diem_tin_cay = r.Confidence,
                thoi_diem = r.Timing,
                ghi_chu = r.Notes,
            },
        };

        return JsonSerializer.Serialize(data, Json);
    }

    private static AnthropicClient Client(AiReportOptions o) => string.IsNullOrWhiteSpace(o.BaseUrl)
        ? new AnthropicClient { ApiKey = o.ApiKey, Timeout = TimeSpan.FromMinutes(o.TimeoutMinutes) }
        : new AnthropicClient { ApiKey = o.ApiKey, Timeout = TimeSpan.FromMinutes(o.TimeoutMinutes), BaseUrl = o.BaseUrl };

    private static MediaType MediaTypeOf(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => MediaType.ImagePng,
        "image/gif" => MediaType.ImageGif,
        "image/webp" => MediaType.ImageWebP,
        _ => MediaType.ImageJpeg,
    };

    private static Effort ParseEffort(string value) => value.Trim().ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "high" => Effort.High,
        "max" => Effort.Max,
        _ => Effort.Medium,
    };

    private async Task<string> SkillAsync(CancellationToken ct)
    {
        if (_skill is null)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Ai", "phan-tich-goc-skill.md");
            if (!File.Exists(path))
            {
                path = Path.Combine(env.ContentRootPath, "Ai", "phan-tich-goc-skill.md");
            }

            _skill = await File.ReadAllTextAsync(path, ct);
        }

        return _skill;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim() is var s && s.Length > 300 ? s[..300] : v.GetString()!.Trim()
            : null;

    private static decimal? Number(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v))
        {
            return null;
        }

        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetDecimal(out var d) => d,
            JsonValueKind.String when decimal.TryParse(v.GetString()?.Trim().TrimEnd('%', '\''), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
    }

    private static int? Int(JsonElement e, string name) => Number(e, name) is { } d && d >= 0 && d <= 500 ? (int)Math.Round(d) : null;

    private static decimal? Line(JsonElement e, string name) =>
        Number(e, name) is { } x && x > 0 && x <= 40 && x * 4 == decimal.Truncate(x * 4) ? x : null;

    private static decimal? Odds(JsonElement e, string name) => Number(e, name) is { } x && x > 1 && x < 50 ? Math.Round(x, 3) : null;
}
