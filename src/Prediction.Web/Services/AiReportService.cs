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

public sealed class AiReportOptions
{
    public const string Section = "Anthropic";

    /// <summary>API key Claude (console.anthropic.com). Đặt bằng user-secrets hoặc biến môi trường Anthropic__ApiKey.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>low / medium / high / max: mức suy nghĩ (càng cao càng kỹ, càng tốn).</summary>
    public string Effort { get; set; } = "high";

    public int MaxTokens { get; set; } = 32000;

    /// <summary>Cho phép Claude tra cứu web (tân binh, HLV, đội hình, chấn thương mới nhất).</summary>
    public bool WebSearch { get; set; } = true;

    public int WebSearchMaxUses { get; set; } = 12;

    /// <summary>Để trống = API Anthropic chính thức (chỉ đổi khi đi qua proxy hoặc kiểm thử).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Thời gian chờ tối đa một báo cáo (phút).</summary>
    public int TimeoutMinutes { get; set; } = 10;

    /// <summary>Model cho "thông tin bên lề" (tra cứu, tóm tắt — dùng model rẻ hơn).</summary>
    public string SideInfoModel { get; set; } = "claude-sonnet-5-5";

    public string SideInfoEffort { get; set; } = "medium";

    public int SideInfoWebSearchMaxUses { get; set; } = 8;

    /// <summary>Tự điền thông tin bên lề cho các trận nhóm A/B trong 24 giờ tới.</summary>
    public bool SideInfoAuto { get; set; } = true;

    /// <summary>Giới hạn chi phí tự động mỗi ngày (USD).</summary>
    public decimal SideInfoDailyBudgetUsd { get; set; } = 3m;

    public int SideInfoMaxPerRun { get; set; } = 10;

    /// <summary>Model cho mục Góc Live (đọc ảnh AiScore, "Claude nhận định").</summary>
    public string CornerModel { get; set; } = "claude-sonnet-5-5";

    public string CornerEffort { get; set; } = "medium";

    public int CornerMaxTokens { get; set; } = 6000;
}

/// <summary>Một cập nhật khi đang nhận báo cáo: đoạn văn bản mới hoặc trạng thái (vd đang tra cứu web).</summary>
public sealed record AiReportUpdate(string? Text, string? Status);

/// <summary>
/// Báo cáo phân tích bằng Claude: system prompt là nguyên file kỹ năng (cache 1 giờ), tin nhắn là toàn bộ số liệu phân tích
/// của trận dạng JSON. Nhận kết quả dạng streaming và lưu vào bảng AiReports.
/// </summary>
public sealed class AiReportService(
    IOptions<AiReportOptions> options,
    IDbContextFactory<AppDbContext> dbFactory,
    AnalysisService analyses,
    MatchContextService contexts,
    IWebHostEnvironment env,
    TimeDisplay time,
    TimeProvider clock,
    AiUsageService usage,
    ILogger<AiReportService> logger)
{
    private const string AppInstructions = """
        Bạn đang chạy bên trong website soi kèo cá nhân của người dùng (Blazor + API-Football). Mỗi yêu cầu gửi kèm số liệu
        đã được website tính sẵn cho MỘT trận đấu (JSON): BXH trước trận, phong độ 10 trận gần nhất hoặc giai đoạn HLV/tân binh mới,
        thống kê phạt góc/thẻ/sút/xG từ API-Football, kèo Bet365 (mở → hiện tại), DNA giải/đội do người dùng gắn nhãn,
        HLV mới/tân binh, danh sách vắng mặt, kèo chấp dự phóng, cờ đỏ, bảng đối chiếu 3 lớp, mô hình Poisson và khuyến nghị
        của website; phần "boi_canh" có các mốc kèo (mở → khi có đội hình → 5 phút trước trận), cảnh báo kèo dịch ≥ 0.5,
        lịch sử kèo đối đầu và 5 trận gần nhất, sân, thời tiết, đội hình ra sân và thông tin bên lề.
        Đây là nguồn định lượng chính: giữ nguyên các con số, không bịa thêm số liệu thống kê.

        Quy ước kèo trong JSON: kèo chấp ghi theo đội nhà (âm = đội nhà chấp, vd -0.75 là đội nhà chấp 3/4 trái);
        tài xỉu/góc/thẻ là mốc tổng. Xác suất là của mô hình website (Poisson cho bàn, nhị thức âm cho góc/thẻ).

        Trả lời bằng tiếng Việt, định dạng Markdown (tiêu đề ##, bảng, gạch đầu dòng), đúng CẤU TRÚC KẾT QUẢ ĐẦU RA ở mục 9,
        đi thẳng vào kết luận, không nhắc lại toàn bộ JSON. Khi dùng thông tin tra cứu web, ghi rõ nguồn.
        Nếu thiếu dữ liệu (vd chưa có kèo góc, chưa có đội hình), nói rõ là thiếu thay vì đoán.

        Xác suất trong khuyến nghị của website đã TRỘN với giá nhà cái (đã bỏ lời), tin mô hình 30–60% tùy số trận mẫu;
        "ly_do" ghi cả số của mô hình thuần lẫn số đã trộn.
        Nếu JSON có "dang_da" thì trận ĐANG ĐÁ (hoặc đã xong): kèo trước trận đã đóng. Khi đó dùng thống kê live
        (sút, góc, xG, thẻ, kiểm soát bóng) và kèo live trong "dang_da" để viết kịch bản In-play cụ thể; phần trước trận chỉ tóm tắt ngắn.

        Tra cứu web: gộp truy vấn (vd "Đội A vs Đội B đội hình chấn thương") để tiết kiệm lượt, ưu tiên đội hình/chấn thương/HLV.
        """;

    private string? _skill;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public string Model => options.Value.Model;

    /// <summary>Bản hoàn chỉnh mới nhất của trận; chưa có bản hoàn chỉnh thì lấy bản dở dang mới nhất.</summary>
    public async Task<AiReport?> LatestAsync(int fixtureId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.AiReports.AsNoTracking().Where(r => r.FixtureId == fixtureId)
            .OrderByDescending(r => r.Complete).ThenByDescending(r => r.CreatedAtUtc).FirstOrDefaultAsync(ct);
    }

    /// <summary>Các bản báo cáo đã viết cho trận (không tải nội dung), mới nhất trước.</summary>
    public async Task<List<AiReportSummary>> HistoryAsync(int fixtureId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.AiReports.AsNoTracking().Where(r => r.FixtureId == fixtureId).OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new AiReportSummary(r.Id, r.CreatedAtUtc, r.Model, r.CostUsd, r.WebSearches, r.Complete, r.StopReason))
            .ToListAsync(ct);
    }

    public async Task<AiReport?> GetAsync(long id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.AiReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    /// <summary>Gọi Claude viết báo cáo cho trận, đẩy từng đoạn chữ qua <paramref name="onUpdate"/> và lưu kết quả.</summary>
    public async Task<AiReport> GenerateAsync(int fixtureId, Func<AiReportUpdate, Task> onUpdate, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Chưa cấu hình Anthropic:ApiKey.");
        }

        var result = await analyses.ForFixtureAsync(fixtureId, ct) ?? throw new InvalidOperationException("Không tìm thấy trận.");
        var live = await LiveDataAsync(result.Fixture, ct);
        var context = await contexts.ForFixtureAsync(fixtureId, ct);
        var o = options.Value;
        var client = string.IsNullOrWhiteSpace(o.BaseUrl)
            ? new AnthropicClient { ApiKey = o.ApiKey, Timeout = TimeSpan.FromMinutes(o.TimeoutMinutes) }
            : new AnthropicClient { ApiKey = o.ApiKey, Timeout = TimeSpan.FromMinutes(o.TimeoutMinutes), BaseUrl = o.BaseUrl };

        var tools = new List<BetaToolUnion>();
        if (o.WebSearch)
        {
            tools.Add(new BetaWebSearchTool20260209 { MaxUses = o.WebSearchMaxUses });
        }

        var parameters = new MessageCreateParams
        {
            Model = o.Model,
            MaxTokens = o.MaxTokens,
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Default(),
            OutputConfig = new BetaOutputConfig { Effort = ParseEffort(o.Effort) },
            System = new List<BetaTextBlockParam>
            {
                new() { Text = await SkillAsync(ct) },
                new() { Text = AppInstructions, CacheControl = new BetaCacheControlEphemeral { Ttl = Ttl.Ttl1h } },
            },
            Tools = tools,
            Messages = [new() { Role = Role.User, Content = UserMessage(result, context, live) }],
        };

        var text = new StringBuilder();
        string? stopReason = null;
        long input = 0, output = 0, cacheRead = 0, cacheWrite = 0, searches = 0;
        var searchErrors = new List<string>();
        var model = o.Model;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var subject = $"{result.Home} – {result.Away}";
        AiUsageLog Log(bool ok, string? error) => new()
        {
            Feature = AiFeature.Report, Model = model, FixtureId = fixtureId, Subject = subject,
            InputTokens = (int)input, OutputTokens = (int)output, CacheReadTokens = (int)cacheRead, CacheWriteTokens = (int)cacheWrite,
            WebSearches = (int)searches, CostUsd = AiPricing.Cost(model, input, output, cacheRead, cacheWrite, searches, cacheWrite1h: true),
            DurationMs = (int)watch.ElapsedMilliseconds, Ok = ok, Error = error,
        };
        AiReport Report(string markdown, bool complete) => new()
        {
            FixtureId = fixtureId,
            Model = model,
            Markdown = markdown,
            StopReason = stopReason,
            InputTokens = (int)input,
            OutputTokens = (int)output,
            CacheReadTokens = (int)cacheRead,
            CacheWriteTokens = (int)cacheWrite,
            WebSearches = (int)searches,
            CostUsd = AiPricing.Cost(model, input, output, cacheRead, cacheWrite, searches, cacheWrite1h: true),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            Complete = complete,
        };

        try
        {
            await foreach (var ev in client.Beta.Messages.CreateStreaming(parameters, ct))
            {
                if (ev.TryPickStart(out var start))
                {
                    model = start.Message.Model.Raw();
                    input = start.Message.Usage.InputTokens;
                    cacheRead = start.Message.Usage.CacheReadInputTokens ?? 0;
                    cacheWrite = start.Message.Usage.CacheCreationInputTokens ?? 0;
                }
                else if (ev.TryPickContentBlockStart(out var blockStart))
                {
                    if (blockStart.ContentBlock.TryPickBetaServerToolUse(out _))
                    {
                        await onUpdate(new AiReportUpdate(null, "Đang tra cứu web…"));
                    }
                    else if (blockStart.ContentBlock.TryPickBetaWebSearchToolResult(out var searchResult) && searchResult.Content.TryPickError(out var searchError))
                    {
                        // Ghi lại lỗi tra cứu (vd max_uses_exceeded) để biết vì sao báo cáo thiếu tin đội hình/chấn thương.
                        var code = searchError.ErrorCode.Raw();
                        searchErrors.Add(code);
                        await onUpdate(new AiReportUpdate(null, $"Tra cứu web lỗi: {code}"));
                    }
                    else if (blockStart.ContentBlock.TryPickBetaText(out _) && text.Length > 0 && text[^1] != '\n')
                    {
                        // Khối văn bản mới (sau một lượt tra cứu): xuống dòng để không dính chữ.
                        text.Append("\n\n");
                    }
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
                    input = messageDelta.Usage.InputTokens ?? input;
                    cacheRead = messageDelta.Usage.CacheReadInputTokens ?? cacheRead;
                    cacheWrite = messageDelta.Usage.CacheCreationInputTokens ?? cacheWrite;
                    searches = messageDelta.Usage.ServerToolUse?.WebSearchRequests ?? searches;
                }
            }
        }
        catch (Exception ex)
        {
            // Phần đã chạy vẫn bị tính tiền: ghi log với số token nhận được tới lúc lỗi, và lưu phần đã viết thành bản dở dang.
            var reason = ex is OperationCanceledException ? "Đã dừng / quá thời gian" : ex.Message;
            await usage.LogAsync(Log(false, reason));
            if (text.Length > 0)
            {
                stopReason ??= ex is OperationCanceledException ? "cancelled" : "error";
                await SavePartialAsync(Report(text.ToString().Trim() + $"\n\n> Bản dở dang: {reason}. Bấm \"Phân tích lại\" để viết bản đầy đủ.", false));
            }

            throw;
        }

        var markdown = text.ToString().Trim();
        if (SearchErrorText(searchErrors) is { } searchNote)
        {
            markdown += $"\n\n> {searchNote}. Báo cáo có thể thiếu tin đội hình / chấn thương; tăng Anthropic:WebSearchMaxUses nếu lỗi là max_uses_exceeded.";
        }

        if (stopReason == "refusal")
        {
            markdown += "\n\n> Claude đã từ chối hoàn thành yêu cầu này (stop_reason = refusal). Hãy thử lại sau.";
        }
        else if (stopReason == "max_tokens")
        {
            markdown += "\n\n> Báo cáo bị cắt do chạm giới hạn độ dài (Anthropic:MaxTokens).";
        }
        else if (stopReason == "pause_turn")
        {
            markdown += "\n\n> Claude dừng giữa chừng sau nhiều lượt tra cứu web. Bấm \"Phân tích lại\" nếu cần bản đầy đủ.";
        }

        var report = Report(markdown, true);

        await using (var db = await dbFactory.CreateDbContextAsync(CancellationToken.None))
        {
            db.AiReports.Add(report);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var logError = string.Join("; ", new[]
        {
            stopReason is "refusal" or "max_tokens" or "pause_turn" ? $"stop_reason = {stopReason}" : null,
            SearchErrorText(searchErrors),
        }.Where(e => e is not null));
        await usage.LogAsync(Log(stopReason is not ("refusal" or "max_tokens"), logError == "" ? null : logError));

        logger.LogInformation("Báo cáo AI trận {Fixture}: {Input}+{CacheRead} cache đọc +{CacheWrite} cache ghi vào, {Output} ra, {Searches} lượt web, ~${Cost}",
            fixtureId, input, cacheRead, cacheWrite, output, searches, report.CostUsd);
        return report;
    }

    /// <summary>"Tra cứu web lỗi: max_uses_exceeded ×3" (null nếu không có lỗi).</summary>
    public static string? SearchErrorText(IReadOnlyCollection<string> errors) => errors.Count == 0
        ? null
        : "Tra cứu web lỗi: " + string.Join(", ", errors.GroupBy(e => e).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key));

    /// <summary>Lưu bản dở dang; lỗi ghi CSDL chỉ ghi log để không che lỗi gốc.</summary>
    private async Task SavePartialAsync(AiReport report)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(CancellationToken.None);
            db.AiReports.Add(report);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không lưu được bản dở dang của báo cáo AI trận {Fixture}", report.FixtureId);
        }
    }

    private static Effort ParseEffort(string value) => value.Trim().ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "medium" => Effort.Medium,
        "max" => Effort.Max,
        _ => Effort.High,
    };

    private async Task<string> SkillAsync(CancellationToken ct)
    {
        if (_skill is null)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Ai", "bong-da-skill.md");
            if (!File.Exists(path))
            {
                path = Path.Combine(env.ContentRootPath, "Ai", "bong-da-skill.md");
            }

            _skill = await File.ReadAllTextAsync(path, ct);
        }

        return _skill;
    }

    // ------------------------------------------------------------------ Dữ liệu gửi cho Claude

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Số liệu live gửi kèm khi bấm báo cáo lúc trận đang đá / đã xong: tỉ số, phút, thống kê, kèo live mới nhất.</summary>
    private async Task<object?> LiveDataAsync(Fixture f, CancellationToken ct)
    {
        if (!f.IsLive && !f.IsFinished)
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var st = await db.FixtureStats.AsNoTracking().FirstOrDefaultAsync(s => s.FixtureId == f.Id, ct);
        var odds = (await db.LiveOddsSnapshots.AsNoTracking().Where(o => o.FixtureId == f.Id).ToListAsync(ct))
            .GroupBy(o => (o.Market, o.FirstHalf))
            .Select(g => g.OrderByDescending(o => o.FetchedAtUtc).First())
            .Select(o => new
            {
                Keo = o.Market == Prediction.Core.Entities.Market.AsianHandicap ? "chap" : "tai_xiu",
                HiepMot = o.FirstHalf ? true : (bool?)null,
                Phut = o.Minute,
                TiSoLucBao = $"{o.HomeGoals}-{o.AwayGoals}",
                MucApi = o.Line,
                MucTuTiSoHienTai = o.Market == Prediction.Core.Entities.Market.AsianHandicap ? o.FromScoreLine : (decimal?)null,
                OddsA = o.PriceA,
                OddsB = o.PriceB,
            })
            .ToList();
        return new
        {
            TrangThai = f.Status,
            Phut = f.Elapsed,
            TiSo = $"{f.HomeGoals}-{f.AwayGoals}",
            ThongKe = st is null ? null : new
            {
                Sut = $"{st.HomeShots}-{st.AwayShots}",
                TrungDich = $"{st.HomeShotsOnTarget}-{st.AwayShotsOnTarget}",
                Goc = $"{st.HomeCorners}-{st.AwayCorners}",
                GocHiep1 = st.FirstHalfHomeCorners is null ? null : $"{st.FirstHalfHomeCorners}-{st.FirstHalfAwayCorners}",
                Xg = st.HomeXg is null ? null : $"{st.HomeXg}-{st.AwayXg}",
                KiemSoatBong = st.HomePossession is null ? null : $"{st.HomePossession}%-{st.AwayPossession}%",
                TheVang = $"{st.HomeYellow}-{st.AwayYellow}",
                TheDo = $"{st.HomeRed ?? 0}-{st.AwayRed ?? 0}",
                BanThang = st.GoalTimeline,
                CapNhatUtc = st.UpdatedAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            },
            KeoLive = odds.Count == 0 ? null : odds,
            GhiChu = "Kèo chấp live của API tính theo tỉ số cả trận; muc_tu_ti_so_hien_tai là mức quy về từ tỉ số lúc báo (kiểu châu Á).",
        };
    }

    private string UserMessage(MatchAnalysisResult r, MatchContextView? context, object? live)
    {
        var data = new { PhanTich = BuildData(r), BoiCanh = context is null ? null : BuildContext(context), DangDa = live };
        var liveNote = live is null ? "" : r.Fixture.IsFinished
            ? "\nLƯU Ý: trận đã kết thúc — hãy đối chiếu khuyến nghị với kết quả và rút kinh nghiệm cho mô hình, không cần kịch bản In-play.\n"
            : $"\nLƯU Ý: trận ĐANG ĐÁ ({r.Fixture.Status}, phút {r.Fixture.Elapsed}, tỉ số {r.Fixture.HomeGoals}-{r.Fixture.AwayGoals}). Kèo trước trận đã đóng: tóm tắt ngắn phần trước trận rồi tập trung vào kịch bản In-play theo số liệu \"dang_da\".\n";
        return $"""
            Hãy phân tích trận {r.Home} – {r.Away} ({r.League?.Name}, {time.Full(r.Fixture.KickoffUtc)} giờ Việt Nam) theo đúng quy trình
            và CẤU TRÚC KẾT QUẢ ĐẦU RA (mục 9) trong hướng dẫn.
            {liveNote}

            Yêu cầu:
            1. Dùng số liệu JSON bên dưới làm nguồn định lượng chính.
            2. Dùng công cụ tìm kiếm web để kiểm tra những gì website không có: tân binh nổi bật, HLV mới và phong cách,
               chấn thương/treo giò mới nhất, đội hình dự kiến, lịch thi đấu giữa tuần, động lực. Ghi rõ nguồn.
            3. Đối chiếu kèo chấp dự phóng với kèo nhà cái, rà 5 cờ đỏ, bảng 3 lớp bàn thắng/phạt góc/chấp góc.
            4. Có thể đồng ý hoặc không với khuyến nghị của website nhưng phải giải thích.
            5. Kết thúc bằng bảng khuyến nghị xếp theo thang điểm 10 (Nhóm A/B/C, xác suất thắng ước tính) và kịch bản In-play.

            ```json
            {JsonSerializer.Serialize(data, Json)}
            ```
            """;
    }

    private static object BuildData(MatchAnalysisResult r)
    {
        var a = r.Analysis;
        var i = r.Input;
        var f = r.Fixture;
        return new
        {
            TranDau = new
            {
                Giai = r.League?.Name,
                QuocGia = r.League?.Country,
                Vong = f.Round,
                GioBongLanUtc = f.KickoffUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                DoiNha = r.Home,
                DoiKhach = r.Away,
                TrangThai = f.Status,
                TiSo = f.HomeGoals.HasValue ? $"{f.HomeGoals}-{f.AwayGoals}" : null,
            },
            Bxh = new
            {
                Nha = Standing(i.HomeStanding),
                Khach = Standing(i.AwayStanding),
                SoDoi = i.TableSize,
            },
            Dna = new
            {
                Giai = a.LeagueTags,
                GiaiGhiChu = a.LeagueNotes,
                Nha = a.HomeTags,
                NhaGhiChu = a.HomeNotes,
                Khach = a.AwayTags,
                KhachGhiChu = a.AwayNotes,
            },
            NhanSu = new
            {
                Nha = Staff(a.HomeRegime, a.HomeAbsent, a.HomeRestDays),
                Khach = Staff(a.AwayRegime, a.AwayAbsent, a.AwayRestDays),
                GhiChu = "Danh sách vắng mặt từ API-Football (có thể chưa đủ); tân binh trụ cột chỉ có nếu người dùng nhập tay.",
            },
            Keo = new
            {
                ChapChauA = Market(i.Handicap),
                TaiXiuBan = Market(i.Totals),
                TaiXiuGoc = Market(i.Corners),
                ChapGoc = Market(i.CornersHandicap),
                TaiXiuThe = Market(i.Cards),
            },
            TrungBinhGiai = new { Ban = Round(i.League.Goals), Goc = Round(i.League.Corners), The = Round(i.League.Cards), SoTran = i.League.Matches, SoTranCoThongKe = i.League.StatMatches },
            PhongDo = new { Nha = Form(a.HomeForm), Khach = Form(a.AwayForm) },
            KeoChapDuPhong = new
            {
                PhanPhongDo = Round(a.Handicap.FormPart),
                PhanBxh = a.Handicap.TablePart is { } t ? Round(t) : (double?)null,
                LoiTheSan = Round(a.Handicap.HomeAdvantage),
                LoiTheSanGhiChu = a.Handicap.HomeAdvantageNotes,
                DoVuotTroiNha = Round(a.Handicap.Supremacy),
                HExp = a.Handicap.HExp,
                HMarket = a.Handicap.HMarket,
                DeltaH = a.Handicap.Delta,
                KeoLa = a.Handicap.IsAnomaly,
                KichBan = a.Handicap.Scenario.ToString(),
                NguyenNhanTimThay = a.Handicap.Causes,
                NhanDinh = a.Handicap.Summary,
            },
            CoDo = a.Flags.Select(x => new { So = x.Number, x.Title, ChiTiet = x.Detail }),
            DoiChieu3Lop = new[] { a.Goals, a.CornersRow, a.CornerHandicap, a.CardsRow }.Select(row => new
            {
                HangMuc = row.Name,
                TbGiai = row.LeagueAverage is { } la ? Round(la) : (double?)null,
                row.LeagueDnaText,
                Nha = row.HomeText,
                Khach = row.AwayText,
                KyVong = row.Expected is { } e ? Round(e) : (double?)null,
                MocKeo = row.MarketLine,
                Delta = row.Delta is { } d ? Round(d) : (double?)null,
                DanhGia = row.Verdict,
            }),
            MoHinh = new
            {
                LambdaNha = Round(a.LambdaHome),
                LambdaKhach = Round(a.LambdaAway),
                GocNha = Round(a.CornersHome),
                GocKhach = Round(a.CornersAway),
                The = Round(a.ExpectedCards),
                NhaThang = Round(a.HomeWinProbability),
                Hoa = Round(a.DrawProbability),
                KhachThang = Round(a.AwayWinProbability),
            },
            KhuyenNghiWebsite = a.Picks.Select(p => new
            {
                Keo = p.Label,
                Odds = p.Odds,
                Nhom = p.Group.ToString(),
                Diem = p.Score,
                XacSuatThang = Round(p.Probability.Win),
                XacSuatThangNua = Round(p.Probability.HalfWin),
                XacSuatHoan = Round(p.Probability.Push),
                XacSuatThuaNua = Round(p.Probability.HalfLoss),
                XacSuatThua = Round(p.Probability.Loss),
                Ev = Round(p.ExpectedValue),
                CoDo = p.Flags,
                LyDo = p.Reasons,
            }),
            KichBanInPlay = a.InPlayHints,
            TuVanApiFootball = i.Advice is { } adv
                ? new { adv.Advice, NhaPct = adv.PercentHome, HoaPct = adv.PercentDraw, KhachPct = adv.PercentAway, TaiXiu = adv.UnderOver }
                : null,
            QuyTacCuaNguoiDungDangChon = i.RuleSides.Select(s => s.ToString()),
            DuLieuIt = a.LowData,
        };
    }

    private static object BuildContext(MatchContextView v)
    {
        object? Point(LinePoint? p) => p is null ? null : new
        {
            Luc = p.AtUtc?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            ChapNha = p.HandicapLine,
            OddsNha = p.HandicapHome,
            OddsKhach = p.HandicapAway,
            TaiXiu = p.TotalLine,
            OddsTai = p.Over,
            OddsXiu = p.Under,
        };

        object Line(HistoryLine h, int teamId) => new
        {
            Ngay = h.KickoffUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Giai = h.LeagueName,
            Tran = $"{h.HomeName} {(h.HasScore ? $"{h.HomeGoals}-{h.AwayGoals}" : "vs")} {h.AwayName}",
            ChapDoiDangXet = h.HandicapFor(teamId),
            TaiXiu = h.TotalLine,
            NguonKeo = h.Origin.ToString(),
        };

        var f = v.Fixture;
        return new
        {
            MocKeo = new { MoKeo = Point(v.Open), KhiCoDoiHinh = Point(v.Lineups), NamPhutTruocTran = Point(v.PreKickoff), HienTai = Point(v.Current) },
            CanhBaoKeo = v.Alerts.Select(a => new { a.Title, ChiTiet = a.Detail }),
            DoiDau = v.HeadToHead.Select(h => Line(h, f.HomeTeamId)),
            NamTranGanNhatNha = v.HomeRecent.Select(h => Line(h, f.HomeTeamId)),
            NamTranGanNhatKhach = v.AwayRecent.Select(h => Line(h, f.AwayTeamId)),
            San = v.Venue is { } venue
                ? new { venue.Name, venue.City, venue.Country, MatSan = venue.Surface, DoCaoM = venue.ElevationM, SucChua = venue.Capacity }
                : null,
            ThoiTiet = v.Weather is { } w
                ? new { NhietDo = w.TemperatureC, MuaMm = w.PrecipitationMm, KhaNangMua = w.PrecipitationProbability, GioKmh = w.WindKph, DoAm = w.HumidityPercent, MoTa = OpenMeteoClient.Describe(w.WeatherCode) }
                : null,
            GhiChuDieuKien = v.Conditions,
            DoiHinh = v.Lineup is { } l
                ? new { Nha = l.HomeStartXI, SoDoNha = l.HomeFormation, Khach = l.AwayStartXI, SoDoKhach = l.AwayFormation }
                : null,
            ThongTinBenLe = v.SideInfo is { } si
                ? new { ThuHan = si.Rivalry, DongLuc = si.Motivation, TinLe = si.News, DacDiemSan = si.VenueNotes, ThoiTiet = si.Weather }
                : null,
        };
    }

    private static object? Standing(TeamSnapshot? s) => s is null
        ? null
        : new { Hang = s.Rank, Tran = s.Played, Diem = s.Points, HieuSo = s.GoalDiff, PhongDo5 = s.Form };

    private static object Staff(RegimeStatus? regime, IReadOnlyList<FixtureInjury> absent, double? rest) => new
    {
        ThayDoi = regime is null
            ? null
            : new { Loai = regime.KindLabel, Ten = regime.PersonName, TuNgay = regime.StartDate.ToString("yyyy-MM-dd"), TranSapDaLaTranThu = regime.UpcomingMatchNumber, ChoOnDinh = regime.InBuffer },
        Vang = absent.Select(x => new { x.PlayerName, x.Type, x.Reason }),
        SoNgayNghi = rest is { } d ? Math.Round(d, 1) : (double?)null,
    };

    private static object? Market(MarketMovement? m) => m is null
        ? null
        : new
        {
            Mo = new { m.Open.Line, OddsA = m.Open.PriceA, OddsB = m.Open.PriceB },
            HienTai = new { m.Current.Line, OddsA = m.Current.PriceA, OddsB = m.Current.PriceB },
            SoLanDoi = m.Changes,
        };

    private static object Form(TeamForm f) => new
    {
        Mau = f.SampleLabel,
        KetQua5 = f.Form,
        Ghi = Round(f.GoalsFor),
        Thung = Round(f.GoalsAgainst),
        TiLeTai25 = Round(f.Over25Rate),
        TiLeHoa = Round(f.DrawRate),
        TiLeThang = Round(f.WinRate),
        GocKiem = f.CornersFor is { } cf ? Round(cf) : (double?)null,
        GocBi = f.CornersAgainst is { } ca ? Round(ca) : (double?)null,
        GocKiemTheoSan = f.VenueCornersFor is { } vf ? Round(vf) : (double?)null,
        GocBiTheoSan = f.VenueCornersAgainst is { } va ? Round(va) : (double?)null,
        TiLeTaiGoc95 = f.Over95CornersRate is { } o ? Round(o) : (double?)null,
        XgTao = f.XgFor is { } xf ? Round(xf) : (double?)null,
        XgLot = f.XgAgainst is { } xa ? Round(xa) : (double?)null,
        Sut = f.ShotsFor is { } sh ? Round(sh) : (double?)null,
        TheNhan = f.CardsFor is { } kf ? Round(kf) : (double?)null,
        TheDoiThu = f.CardsAgainst is { } ka ? Round(ka) : (double?)null,
        TiLeThangKeoChap = f.AtsRate is { } ats ? Round(ats) : (double?)null,
        SoTranCoKeo = f.AtsMatches,
    };

    private static double Round(double value) => Math.Round(value, 2);
}

/// <summary>Một bản báo cáo AI đã lưu (không kèm nội dung) cho danh sách "Các bản đã viết".</summary>
public sealed record AiReportSummary(long Id, DateTime CreatedAtUtc, string Model, decimal CostUsd, int WebSearches, bool Complete, string? StopReason);
