using Prediction.Web.ApiFootball;

namespace Prediction.Web.Services;

/// <summary>
/// Danh sách giải đang diễn ra kèm độ phủ dữ liệu (<c>leagues?current=true</c>, ~1.200 giải / 1 request) cho Góc Live toàn cầu.
/// Giữ trong bộ nhớ, tải lại sau <see cref="MaxAge"/>.
/// </summary>
public sealed class LeagueCoverageCache
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    private Dictionary<int, LeagueItem> _leagues = [];
    private HashSet<int> _stats = [];

    public DateTime? FetchedAtUtc { get; private set; }

    /// <summary>Id các giải API có thống kê trận (góc, sút…) mùa hiện tại.</summary>
    public IReadOnlySet<int> StatsLeagueIds => _stats;

    public bool IsStale(DateTime nowUtc) => FetchedAtUtc is not { } at || nowUtc - at >= MaxAge;

    public LeagueItem? Get(int leagueId) => _leagues.GetValueOrDefault(leagueId);

    public void Set(IEnumerable<LeagueItem> items, DateTime nowUtc)
    {
        var leagues = items.GroupBy(i => i.League.Id).ToDictionary(g => g.Key, g => g.First());
        _stats = leagues.Values.Where(HasStats).Select(i => i.League.Id).ToHashSet();
        _leagues = leagues;
        FetchedAtUtc = nowUtc;
    }

    public static bool HasStats(LeagueItem item) =>
        (item.Seasons.FirstOrDefault(s => s.Current) ?? item.Seasons.OrderByDescending(s => s.Year).FirstOrDefault())
            ?.Coverage?.Fixtures?.StatisticsFixtures == true;
}
