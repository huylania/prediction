using System.Text.Json;
using Prediction.Web.ApiFootball;
using Prediction.Web.Services;

namespace Prediction.Tests;

public class GlobalLiveTests
{
    private static List<T> Load<T>(string file)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", file));
        var envelope = JsonSerializer.Deserialize<ApiEnvelope>(json, ApiFootballClient.Json)!;
        return envelope.Response.Deserialize<List<T>>(ApiFootballClient.Json)!;
    }

    private static readonly DateTime Now = new(2026, 10, 17, 13, 10, 0, DateTimeKind.Utc);

    [Fact]
    public void Coverage_cache_keeps_leagues_with_fixture_statistics()
    {
        // leagues?current=true thật (rút gọn): Damallsvenskan 549, Eliteserien 103, Premier League 39 có thống kê; 2. Division 473 thì không.
        var cache = new LeagueCoverageCache();
        Assert.True(cache.IsStale(Now));
        cache.Set(Load<LeagueItem>("leagues-current.json"), Now);

        Assert.Equal(new HashSet<int> { 39, 103, 549 }, cache.StatsLeagueIds.ToHashSet());
        Assert.Equal("Damallsvenskan", cache.Get(549)!.League.Name);
        Assert.False(cache.IsStale(Now.AddHours(23)));
        Assert.True(cache.IsStale(Now.AddHours(24)));
    }

    [Fact]
    public void Global_scan_picks_live_matches_of_untracked_stats_leagues()
    {
        // fixtures?live=all thật lúc 13:10 UTC thứ Bảy 17/10 (rút gọn còn 6 trận).
        var cache = new LeagueCoverageCache();
        cache.Set(Load<LeagueItem>("leagues-current.json"), Now);
        var live = Load<FixtureItem>("fixtures-live-all.json");

        var picked = DataSyncService.GlobalLiveFixtures(live, cache.StatsLeagueIds, new HashSet<int> { 39 });

        // Bỏ Premier League (giải theo dõi, đi quy trình thường) và 2. Division (không có thống kê); giữ cả trận đang nghỉ giữa hiệp.
        Assert.Equal([1495584, 1495582, 1494780], picked.Select(i => i.Fixture.Id));
        Assert.Contains(picked, i => i.Fixture.Status.Short == "HT");
        Assert.All(picked, i => Assert.True(i.Statistics is null or { Count: 0 })); // live=all không kèm thống kê: phải lấy thêm theo id
    }
}
