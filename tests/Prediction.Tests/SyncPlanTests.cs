using Microsoft.Extensions.Configuration;
using Prediction.Core.Engine.LiveCorners;
using Prediction.Web.Services;

namespace Prediction.Tests;

public class SyncPlanTests
{
    [Fact]
    public void Auto_uses_ultra_values_when_daily_limit_is_large()
    {
        var pro = new SyncOptions();
        var o = pro.For(75000);

        Assert.NotSame(pro, o);
        Assert.Equal("Ultra", pro.PlanName(75000));
        Assert.Equal(20, o.OddsIntervalMinutes);
        Assert.Equal(1, o.LiveIntervalMinutes);
        Assert.Equal(1, o.LiveOddsIntervalMinutes);
        Assert.Equal(1, o.LiveCornersIntervalMinutes);
        Assert.Equal(20, o.LiveCornerFastSeconds);
        Assert.Equal(200, o.StatsMaxRequests);
        Assert.Equal(5000, o.QuotaReserve);
        Assert.Equal(3000, o.LiveCornerFastMinRemaining);
        Assert.True(o.LiveOddsFallbackAllLive);
        Assert.Equal(30, o.LiveOddsFallbackMaxLeagues);
        Assert.False(pro.LiveOddsFallbackAllLive);

        // Các giá trị khác giữ nguyên, bản gốc không bị sửa.
        Assert.Equal(pro.ClosingOddsIntervalMinutes, o.ClosingOddsIntervalMinutes);
        Assert.Equal(pro.HotZone, o.HotZone);
        Assert.Equal(60, pro.OddsIntervalMinutes);
        Assert.Equal(1000, pro.QuotaReserve);
    }

    [Fact]
    public void Auto_keeps_pro_values_for_small_or_unknown_limit()
    {
        var pro = new SyncOptions();
        Assert.Same(pro, pro.For(7500));
        Assert.Same(pro, pro.For(null));
        Assert.Same(pro, pro.For(49999));
        Assert.Equal("Pro", pro.PlanName(7500));
        Assert.Equal(30, pro.For(7500).LiveCornerFastSeconds);
    }

    [Fact]
    public void Mega_limit_turns_on_mega_profile()
    {
        var pro = new SyncOptions();
        Assert.Equal(SyncTier.Mega, pro.Tier(150000));
        Assert.Equal(SyncTier.Ultra, pro.Tier(99999));
        var o = pro.For(150000);
        Assert.Equal(15, o.LiveCornerFastSeconds);
        Assert.Equal(400, o.StatsMaxRequests);
        Assert.Equal(3, o.StatsPreviousSeasons);
        Assert.Equal(10000, o.QuotaReserve);
        Assert.Equal(5000, o.LiveCornerFastMinRemaining);
        Assert.Equal(60, o.LiveOddsFallbackMaxLeagues);
        Assert.True(o.LiveCornerGlobal);
        Assert.Equal(20, o.OddsIntervalMinutes); // kèo trước trận API vài giờ mới đổi: không quét dày hơn Ultra
        Assert.False(pro.For(75000).LiveCornerGlobal);
        Assert.Equal(1, pro.For(75000).StatsPreviousSeasons);
        Assert.Equal(SyncTier.Mega, new SyncOptions { Plan = "Mega" }.Tier(7500));
    }

    [Fact]
    public void Config_overrides_one_value_and_keeps_tier_defaults()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sync:Mega:LiveCornerFastSeconds"] = "12",
            ["Sync:Ultra:MinDailyLimit"] = "60000",
        }).Build();
        var o = config.GetSection(SyncOptions.Section).Get<SyncOptions>()!;
        Assert.Equal(12, o.Mega.LiveCornerFastSeconds);
        Assert.Equal(100000, o.Mega.MinDailyLimit);
        Assert.Equal(400, o.Mega.StatsMaxRequests);
        Assert.Equal(60000, o.Ultra.MinDailyLimit);
        Assert.Equal(20, o.Ultra.LiveCornerFastSeconds);
    }

    [Fact]
    public void Plan_setting_forces_a_profile()
    {
        Assert.Equal(60, new SyncOptions { Plan = "Pro" }.For(75000).OddsIntervalMinutes);
        Assert.Equal(20, new SyncOptions { Plan = "Ultra" }.For(null).OddsIntervalMinutes);
        Assert.Equal(20, new SyncOptions { Plan = " ultra " }.For(7500).OddsIntervalMinutes);
    }

    [Fact]
    public void Ultra_values_and_threshold_come_from_config()
    {
        var ultra = SyncTierOptions.UltraDefaults();
        ultra.MinDailyLimit = 30000;
        ultra.OddsIntervalMinutes = 15;
        var o = new SyncOptions { Ultra = ultra };
        Assert.Equal(15, o.For(30000).OddsIntervalMinutes);
        Assert.Same(o, o.For(29999));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(30)]
    public void Fast_cycle_runs_at_the_configured_cadence(int seconds)
    {
        // Mô phỏng vòng kiểm tra của LiveCornerFastService: khoảng cách giữa hai chu kỳ phải đúng số giây cấu hình.
        var t0 = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        DateTime? last = null;
        var runs = new List<DateTime>();
        for (var now = t0; now < t0.AddMinutes(3); now += LiveCornerFastService.CheckEvery)
        {
            if (LiveClock.ShouldRunFast(seconds, 1, 70000, 3000, last, now))
            {
                last = now;
                runs.Add(now);
            }
        }

        Assert.All(runs.Zip(runs.Skip(1)), p => Assert.Equal(seconds, (p.Second - p.First).TotalSeconds));
    }

    [Fact]
    public void Live_odds_fallback_calls_only_leagues_with_missing_wanted_fixtures()
    {
        var live = new[]
        {
            (Id: 1, LeagueId: 39, Wanted: true),
            (Id: 2, LeagueId: 40, Wanted: true),
            (Id: 3, LeagueId: 40, Wanted: true),
            (Id: 4, LeagueId: 140, Wanted: true), // gọi chung đã trả về
            (Id: 5, LeagueId: 78, Wanted: false), // không cần kèo (vd giờ nghỉ)
        };

        var leagues = DataSyncService.LiveOddsFallbackLeagues(live, new HashSet<int> { 4 }, 10);
        Assert.Equal([40, 39], leagues); // giải thiếu nhiều trận nhất trước

        Assert.Equal([40], DataSyncService.LiveOddsFallbackLeagues(live, new HashSet<int> { 4 }, 1));
        Assert.Empty(DataSyncService.LiveOddsFallbackLeagues(live, new HashSet<int> { 1, 2, 3, 4 }, 10));
        Assert.Empty(DataSyncService.LiveOddsFallbackLeagues(live, new HashSet<int>(), 0));
    }

    [Fact]
    public void Request_pacing_follows_the_plan_rate_limit()
    {
        var o = new Prediction.Web.ApiFootball.ApiFootballOptions();
        double Ms(int? limit, int? remaining = null) => Prediction.Web.ApiFootball.ApiFootballClient.RequestDelay(o, limit, remaining).TotalMilliseconds;

        Assert.Equal(300, Ms(null));  // chưa biết gói
        Assert.Equal(334, Ms(300));   // Pro 300/phút → ~180/phút
        Assert.Equal(223, Ms(450));   // Ultra 450/phút → ~270/phút
        Assert.Equal(150, Ms(900));   // Mega 900/phút → chặn ở 150 ms (~400/phút)
        Assert.Equal(1000, Ms(900, 50)); // phút này gần hết → chậm lại
        Assert.Equal(150, Ms(900, 500));
    }

    [Fact]
    public void Applying_twice_gives_the_same_values()
    {
        var once = new SyncOptions().For(75000);
        var twice = once.For(75000);
        Assert.Equal(once.OddsIntervalMinutes, twice.OddsIntervalMinutes);
        Assert.Equal(once.QuotaReserve, twice.QuotaReserve);
        Assert.Equal(once.StatsMaxRequests, twice.StatsMaxRequests);
    }
}
