using System.Text;
using Microsoft.EntityFrameworkCore;
using Prediction.Core.Engine.Analysis;
using Prediction.Core.Entities;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>Kết quả kiểm DNA của một đội / giải.</summary>
public sealed record DnaCheckResult(int Id, string Name, DnaTag Tags, DnaStats Stats, IReadOnlyList<DnaTagCheck> Checks)
{
    public DnaTag Contradicted => DnaVerifier.Contradicted(Checks);

    public int Count(DnaCheckStatus status) => Checks.Count(c => c.Status == status);
}

public sealed record DnaCheckReport(
    DateTime ComputedAtUtc,
    DateTime FromUtc,
    int MatchCount,
    IReadOnlyDictionary<int, DnaCheckResult> Teams,
    IReadOnlyDictionary<int, DnaCheckResult> Leagues);

/// <summary>
/// Kiểm chứng DNA đội / giải bằng các trận đã đá trong 1 năm qua mà website đã lấy từ API-Football (không gọi API thêm).
/// Tích ngược số liệu có thể gỡ bằng nút trên trang DNA hoặc bằng script SQL tải về.
/// </summary>
public sealed class DnaCheckService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DnaCheckReport? _cached;

    public async Task<DnaCheckReport> ReportAsync(bool refresh = false, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (!refresh && _cached is { } c && now - c.ComputedAtUtc < CacheFor)
        {
            return c;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (!refresh && _cached is { } again && now - again.ComputedAtUtc < CacheFor)
            {
                return again;
            }

            return _cached = await ComputeAsync(now, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<DnaCheckReport> ComputeAsync(DateTime now, CancellationToken ct)
    {
        var from = now.AddDays(-365);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Fixtures.AsNoTracking()
            .Where(f => f.KickoffUtc >= from && f.KickoffUtc <= now && f.HomeGoals != null && f.AwayGoals != null
                && (f.Status == "FT" || f.Status == "AET" || f.Status == "PEN") && !f.League!.IsDemo
                && (f.League.IsTracked || !f.League.LiveScan)) // trận Góc Live toàn cầu chỉ có số liệu dở dang
            .GroupJoin(db.FixtureStats.AsNoTracking(), f => f.Id, s => s.FixtureId, (f, s) => new { f, s })
            .SelectMany(x => x.s.DefaultIfEmpty(), (x, s) => new
            {
                x.f.LeagueId,
                x.f.HomeTeamId,
                x.f.AwayTeamId,
                HomeGoals = x.f.HomeGoals!.Value,
                AwayGoals = x.f.AwayGoals!.Value,
                HtHome = s == null ? null : s.HalftimeHome,
                HtAway = s == null ? null : s.HalftimeAway,
                CornersHome = s == null ? null : s.HomeCorners,
                CornersAway = s == null ? null : s.AwayCorners,
                YellowHome = s == null ? null : s.HomeYellow,
                YellowAway = s == null ? null : s.AwayYellow,
                RedHome = s == null ? null : s.HomeRed,
                RedAway = s == null ? null : s.AwayRed,
                Timeline = s == null ? null : s.GoalTimeline,
                HasStats = s != null,
            })
            .ToListAsync(ct);

        var teamMatches = new Dictionary<int, List<VerifyMatch>>();
        var leagueMatches = new Dictionary<int, List<VerifyMatch>>();
        foreach (var r in rows)
        {
            int? cardsHome = r.YellowHome is { } yh ? yh + (r.RedHome ?? 0) : null;
            int? cardsAway = r.YellowAway is { } ya ? ya + (r.RedAway ?? 0) : null;
            bool? late = r.HasStats ? DnaVerifier.LateGoal(r.HomeGoals + r.AwayGoals, r.Timeline) : null;
            var home = new VerifyMatch(true, r.HomeGoals, r.AwayGoals, r.HtHome, r.HtAway, r.CornersHome, r.CornersAway, cardsHome, cardsAway, late);
            var away = new VerifyMatch(false, r.AwayGoals, r.HomeGoals, r.HtAway, r.HtHome, r.CornersAway, r.CornersHome, cardsAway, cardsHome, late);
            Add(teamMatches, r.HomeTeamId, home);
            Add(teamMatches, r.AwayTeamId, away);
            Add(leagueMatches, r.LeagueId, home);
        }

        var teams = new Dictionary<int, DnaCheckResult>();
        foreach (var d in await db.TeamDnas.AsNoTracking().ToListAsync(ct))
        {
            var stats = DnaVerifier.Stats(teamMatches.GetValueOrDefault(d.TeamId) ?? []);
            teams[d.TeamId] = new DnaCheckResult(d.TeamId, d.TeamName, d.Tags, stats, DnaVerifier.CheckTeam(d.Tags, stats));
        }

        var leagues = new Dictionary<int, DnaCheckResult>();
        foreach (var d in await db.LeagueDnas.AsNoTracking().ToListAsync(ct))
        {
            var stats = DnaVerifier.Stats(leagueMatches.GetValueOrDefault(d.LeagueId) ?? []);
            leagues[d.LeagueId] = new DnaCheckResult(d.LeagueId, d.LeagueName, d.Tags, stats, DnaVerifier.CheckLeague(d.Tags, stats));
        }

        return new DnaCheckReport(now, from, rows.Count, teams, leagues);
    }

    /// <summary>Gỡ các tích bị số liệu bác khỏi CSDL (giữ nguyên tích khác và ghi chú). Trả về số đội / giải đã sửa.</summary>
    public async Task<(int Teams, int Leagues)> RemoveContradictedAsync(DnaCheckReport report, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var teams = 0;
        foreach (var (id, mask) in report.Teams.Values.Where(r => r.Contradicted != DnaTag.None).Select(r => (r.Id, r.Contradicted)))
        {
            teams += await db.TeamDnas.Where(d => d.TeamId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Tags, d => d.Tags & ~mask), ct);
        }

        var leagues = 0;
        foreach (var (id, mask) in report.Leagues.Values.Where(r => r.Contradicted != DnaTag.None).Select(r => (r.Id, r.Contradicted)))
        {
            leagues += await db.LeagueDnas.Where(d => d.LeagueId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Tags, d => d.Tags & ~mask), ct);
        }

        _cached = null;
        return (teams, leagues);
    }

    /// <summary>Script SQL gỡ tích ngược số liệu (để chạy tay bằng SSMS), mỗi đội / giải một lệnh UPDATE có chú thích.</summary>
    public static string SqlScript(DnaCheckReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"-- Gỡ các tích DNA bị số liệu API-Football 1 năm bác ({report.FromUtc:dd/MM/yyyy} – {report.ComputedAtUtc:dd/MM/yyyy}, {report.MatchCount} trận).");
        sb.AppendLine("-- Sinh bởi trang DNA của website. Chỉ bỏ đúng các tích ghi trong chú thích, giữ nguyên tích khác và ghi chú.");
        sb.AppendLine("-- Chạy lại nhiều lần vẫn cho cùng kết quả (phép AND NOT).");
        sb.AppendLine("SET NOCOUNT ON;");
        sb.AppendLine("BEGIN TRANSACTION;");
        foreach (var r in report.Teams.Values.Where(r => r.Contradicted != DnaTag.None).OrderBy(r => r.Name))
        {
            sb.AppendLine($"UPDATE dbo.TeamDnas SET Tags = Tags & ~{(int)r.Contradicted} WHERE TeamId = {r.Id}; -- {Comment(r, true)}");
        }

        foreach (var r in report.Leagues.Values.Where(r => r.Contradicted != DnaTag.None).OrderBy(r => r.Name))
        {
            sb.AppendLine($"UPDATE dbo.LeagueDnas SET Tags = Tags & ~{(int)r.Contradicted} WHERE LeagueId = {r.Id}; -- {Comment(r, false)}");
        }

        sb.AppendLine("COMMIT TRANSACTION;");
        return sb.ToString();
    }

    private static string Comment(DnaCheckResult r, bool team) =>
        $"{r.Name.Replace("\n", " ")}: bỏ {string.Join(", ", r.Checks.Where(c => c.Status == DnaCheckStatus.Contradict).Select(c => DnaTagText.Label(c.Tag, team)))}";

    private static void Add(Dictionary<int, List<VerifyMatch>> map, int key, VerifyMatch m)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = [];
        }

        list.Add(m);
    }
}
