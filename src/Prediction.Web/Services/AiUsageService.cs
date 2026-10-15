using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prediction.Core.Entities;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>Bảng giá Claude (USD / triệu token) để ước tính chi phí. Số chính xác xem ở console.anthropic.com.</summary>
public static class AiPricing
{
    public const decimal WebSearchUsd = 0.01m;

    /// <summary>Giá (vào, ra) theo dòng model.</summary>
    public static (decimal Input, decimal Output) Prices(string model) => model switch
    {
        var m when m.Contains("opus", StringComparison.OrdinalIgnoreCase) => (4m, 20m),
        var m when m.Contains("haiku", StringComparison.OrdinalIgnoreCase) => (1m, 5m),
        var m when m.Contains("fable", StringComparison.OrdinalIgnoreCase) || m.Contains("mythos", StringComparison.OrdinalIgnoreCase) => (10m, 50m),
        _ => (2m, 10m),
    };

    /// <summary>
    /// Chi phí một lần gọi: token vào thường + ghi cache (5 phút ×1.25, 1 giờ ×2 giá vào) + đọc cache (×0.1) + token ra + lượt tra web.
    /// </summary>
    public static decimal Cost(string model, long input, long output, long cacheRead, long cacheWrite, long searches, bool cacheWrite1h = false)
    {
        var (inPrice, outPrice) = Prices(model);
        var writeFactor = cacheWrite1h ? 2m : 1.25m;
        return Math.Round((input * inPrice + output * outPrice + cacheWrite * inPrice * writeFactor + cacheRead * inPrice * 0.1m) / 1_000_000m
            + searches * WebSearchUsd, 4);
    }
}

/// <summary>Cài đặt AI tự động (ghi đè appsettings, đổi được trên trang Chi phí AI).</summary>
public sealed record AiAutoSettings(bool SideInfoAuto, decimal SideInfoDailyBudgetUsd);

/// <summary>Ghi và tổng hợp log chi phí API Claude của mọi tính năng.</summary>
public sealed class AiUsageService(
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<AiReportOptions> options,
    TimeProvider clock,
    ILogger<AiUsageService> logger)
{
    public const string SideInfoAutoKey = "Ai.SideInfoAuto";
    public const string SideInfoBudgetKey = "Ai.SideInfoDailyBudgetUsd";

    private AiAutoSettings? _settings;

    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    /// <summary>Ghi một lần gọi Claude. Không bao giờ làm hỏng tính năng đang chạy nếu ghi log lỗi.</summary>
    public async Task LogAsync(AiUsageLog entry)
    {
        try
        {
            entry.CreatedAtUtc = entry.CreatedAtUtc == default ? UtcNow : entry.CreatedAtUtc;
            entry.Subject = entry.Subject is { Length: > 200 } s ? s[..200] : entry.Subject;
            entry.Error = entry.Error is { Length: > 500 } e ? e[..500] : entry.Error;
            await using var db = await dbFactory.CreateDbContextAsync(CancellationToken.None);
            db.AiUsageLogs.Add(entry);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không ghi được log chi phí Claude ({Feature}, ~${Cost})", entry.Feature, entry.CostUsd);
        }
    }

    public async Task<AiAutoSettings> SettingsAsync(CancellationToken ct = default)
    {
        if (_settings is { } cached)
        {
            return cached;
        }

        var o = options.Value;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.AppSettings.AsNoTracking().Where(s => s.Key == SideInfoAutoKey || s.Key == SideInfoBudgetKey).ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        var auto = rows.GetValueOrDefault(SideInfoAutoKey) is { } a && bool.TryParse(a, out var on) ? on : o.SideInfoAuto;
        var budget = rows.GetValueOrDefault(SideInfoBudgetKey) is { } b && decimal.TryParse(b, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
            ? v
            : o.SideInfoDailyBudgetUsd;
        return _settings = new AiAutoSettings(auto, budget);
    }

    public async Task SaveSettingsAsync(AiAutoSettings settings, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        foreach (var (key, value) in new[]
                 {
                     (SideInfoAutoKey, settings.SideInfoAuto.ToString()),
                     (SideInfoBudgetKey, Math.Max(0, settings.SideInfoDailyBudgetUsd).ToString(CultureInfo.InvariantCulture)),
                 })
        {
            var row = await db.AppSettings.FindAsync([key], ct);
            if (row is null)
            {
                db.AppSettings.Add(new AppSetting { Key = key, Value = value });
            }
            else
            {
                row.Value = value;
            }
        }

        await db.SaveChangesAsync(ct);
        _settings = settings with { SideInfoDailyBudgetUsd = Math.Max(0, settings.SideInfoDailyBudgetUsd) };
    }

    /// <summary>Tổng chi phí của một tính năng từ mốc thời gian (gồm cả lần lỗi).</summary>
    public async Task<decimal> SpentAsync(AiFeature feature, DateTime fromUtc, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.AiUsageLogs.Where(l => l.Feature == feature && l.CreatedAtUtc >= fromUtc).SumAsync(l => (decimal?)l.CostUsd, ct) ?? 0;
    }

    public async Task<List<AiUsageLog>> LogsAsync(DateTime? fromUtc, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.AiUsageLogs.AsNoTracking()
            .Where(l => fromUtc == null || l.CreatedAtUtc >= fromUtc)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(ct);
    }
}
