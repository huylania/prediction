using Microsoft.EntityFrameworkCore;
using Prediction.Core.Entities;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>
/// Tạo "Giải Demo" với kết quả, kèo biến động và tư vấn GIẢ LẬP để xem thử website khi chưa có API key.
/// Mọi dữ liệu demo nằm trong giải id 900001 và xóa được ở trang Cài đặt.
/// </summary>
public sealed class DemoDataSeeder(IDbContextFactory<AppDbContext> dbFactory, SuggestionService suggestions, TimeProvider clock)
{
    public const int LeagueId = 900001;
    private const int TeamBase = 900100;
    private const int FixtureBase = 900000000;

    private static readonly string[] TeamNames =
    [
        "Rồng Đỏ FC", "Sư Tử Xanh", "Đại Bàng Vàng", "Hải Cảng", "Sông Hồng", "Núi Bạc",
        "Phố Cổ United", "Sao Biển", "Cao Nguyên", "Đồng Bằng", "Thủ Đô City", "Gió Lào",
    ];

    public async Task<string> SeedAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await db.Leagues.AnyAsync(l => l.Id == LeagueId, ct))
        {
            return "Dữ liệu demo đã có sẵn.";
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var random = new Random(2026);

        db.Leagues.Add(new League
        {
            Id = LeagueId,
            Name = "Giải Demo (dữ liệu giả lập)",
            Country = "Demo",
            Type = "League",
            CurrentSeason = 2026,
            IsTracked = true,
            IsDemo = true,
        });

        var strength = new Dictionary<int, double>();
        for (var i = 0; i < TeamNames.Length; i++)
        {
            var id = TeamBase + i + 1;
            db.Teams.Add(new Team { Id = id, Name = TeamNames[i] });
            strength[id] = 0.6 - 1.2 * i / (TeamNames.Length - 1);
        }

        var teamIds = strength.Keys.ToList();
        var fixtureId = FixtureBase;

        foreach (var season in new[] { 2025, 2026 })
        {
            var rounds = DoubleRoundRobin(teamIds);
            for (var r = 0; r < rounds.Count; r++)
            {
                var roundStart = season == 2025
                    ? now.Date.AddDays(-330 + r * 7).AddHours(12)
                    : r < 9 ? now.Date.AddDays(-(9 - r) * 7).AddHours(12) : now.AddHours(3 + (r - 9) * 7 * 24);

                for (var m = 0; m < rounds[r].Count; m++)
                {
                    var (home, away) = rounds[r][m];
                    // Vòng 10 mùa này: 2 trận đang đá, 1 trận vừa đá xong hôm nay, còn lại sắp đá.
                    var kickoff = season == 2026 && r == 9
                        ? m switch
                        {
                            0 => now.AddMinutes(-70),
                            1 => now.AddMinutes(-28),
                            2 => now.AddHours(-4),
                            _ => Round(now.AddHours(3 + (m - 3) * 7)),
                        }
                        : Round(roundStart.AddHours(m * 7));
                    var fixture = new Fixture
                    {
                        Id = ++fixtureId,
                        LeagueId = LeagueId,
                        Season = season,
                        Round = $"Regular Season - {r + 1}",
                        KickoffUtc = kickoff,
                        HomeTeamId = home,
                        AwayTeamId = away,
                        HasTable = true,
                        Status = FixtureStatus.NotStarted,
                        UpdatedAtUtc = now,
                    };

                    var lambdaHome = 1.25 * Math.Exp(0.9 * (strength[home] - strength[away])) + 0.15;
                    var lambdaAway = 1.05 * Math.Exp(0.9 * (strength[away] - strength[home]));
                    if (kickoff.AddHours(2) < now)
                    {
                        fixture.Status = FixtureStatus.FullTime;
                        fixture.HomeGoals = Poisson(random, lambdaHome);
                        fixture.AwayGoals = Poisson(random, lambdaAway);
                    }
                    else if (kickoff < now)
                    {
                        // Đang đá: phút thi đấu tính cả 15 phút nghỉ giữa hiệp.
                        var minutes = (int)(now - kickoff).TotalMinutes;
                        fixture.Status = minutes <= 45 ? "1H" : minutes < 60 ? "HT" : "2H";
                        fixture.Elapsed = minutes <= 45 ? minutes : minutes < 60 ? 45 : Math.Min(90, minutes - 15);
                        var share = fixture.Elapsed.Value / 90.0;
                        fixture.HomeGoals = Poisson(random, lambdaHome * share);
                        fixture.AwayGoals = Poisson(random, lambdaAway * share);
                    }

                    db.Fixtures.Add(fixture);

                    // Mùa trước không có kèo (giống thực tế API chỉ giữ kèo ~7 ngày); mùa này có kèo + tư vấn.
                    if (season == 2026 && r < 11)
                    {
                        AddOdds(db, random, fixture, lambdaHome, lambdaAway, now);
                        AddAdvice(db, random, fixture, lambdaHome, lambdaAway, now);
                    }
                }
            }
        }

        db.Rules.AddRange(
            new Rule
            {
                Name = "Demo: đội xếp trên ≥ 4 bậc đá sân nhà → chọn đội nhà",
                Notes = "Quy tắc mẫu cho Giải Demo.",
                LeagueId = LeagueId,
                Market = Market.AsianHandicap,
                Selection = RuleSelection.Subject,
                SubjectVenue = VenueFilter.Home,
                MinRankGap = 4,
                MinMatchesPlayed = 3,
                CreatedAtUtc = now,
            },
            new Rule
            {
                Name = "Demo: tiền đổ về đội khách → theo đội khách",
                Notes = "Theo biến động kèo: mức chấp dịch về phía đội khách hoặc tiền đội khách giảm.",
                LeagueId = LeagueId,
                Market = Market.AsianHandicap,
                Selection = RuleSelection.Subject,
                SubjectVenue = VenueFilter.Away,
                AhMovement = AhMovementFilter.TowardSubject,
                CreatedAtUtc = now,
            },
            new Rule
            {
                Name = "Demo: hai đội đá cởi mở → Tài",
                Notes = "Trung bình tổng bàn 5 trận gần nhất của hai đội ≥ 3.",
                LeagueId = LeagueId,
                Market = Market.OverUnder,
                Selection = RuleSelection.Over,
                MinAvgTotalGoals = 3m,
                CreatedAtUtc = now,
            });

        await db.SaveChangesAsync(ct);
        await suggestions.BackfillAsync(LeagueId, ct);
        await suggestions.RefreshAsync(ct);
        return "Đã tạo dữ liệu demo.";
    }

    public async Task<string> DeleteAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Rules.Where(r => r.LeagueId == LeagueId).ExecuteDeleteAsync(ct);
        await db.Fixtures.Where(f => f.LeagueId == LeagueId).ExecuteDeleteAsync(ct);
        await db.Teams.Where(t => t.Id > TeamBase && t.Id <= TeamBase + TeamNames.Length).ExecuteDeleteAsync(ct);
        await db.Leagues.Where(l => l.Id == LeagueId).ExecuteDeleteAsync(ct);
        return "Đã xóa dữ liệu demo.";
    }

    private static void AddOdds(AppDbContext db, Random random, Fixture fixture, double lambdaHome, double lambdaAway, DateTime now)
    {
        var lastAllowed = fixture.KickoffUtc.AddMinutes(-10) < now ? fixture.KickoffUtc.AddMinutes(-10) : now;
        var at = fixture.KickoffUtc.AddDays(-5);
        if (at > lastAllowed)
        {
            at = lastAllowed.AddHours(-6);
        }

        var homeLine = Quarter(-(lambdaHome - lambdaAway) * 0.9 + (random.NextDouble() - 0.5) * 0.3, -2.5m, 2.5m);
        var homePrice = 1.85m + (decimal)Math.Round(random.NextDouble() * 0.2, 2);
        var totalLine = Quarter(lambdaHome + lambdaAway + (random.NextDouble() - 0.5) * 0.4, 1.75m, 3.75m);
        var overPrice = 1.85m + (decimal)Math.Round(random.NextDouble() * 0.2, 2);

        var steps = random.Next(1, 6);
        var span = (lastAllowed - at) / steps;
        for (var i = 0; i <= steps; i++)
        {
            if (i > 0)
            {
                // Mỗi bước: đôi khi dịch mức kèo 1/4, thường chỉ dịch tiền kèo.
                if (random.NextDouble() < 0.25)
                {
                    homeLine += random.NextDouble() < 0.5 ? -0.25m : 0.25m;
                    homePrice = 1.95m;
                }
                else
                {
                    homePrice = Math.Clamp(homePrice + (decimal)Math.Round((random.NextDouble() - 0.5) * 0.16, 2), 1.70m, 2.15m);
                }

                if (random.NextDouble() < 0.2)
                {
                    totalLine = Math.Clamp(totalLine + (random.NextDouble() < 0.5 ? -0.25m : 0.25m), 1.5m, 4m);
                    overPrice = 1.95m;
                }
                else
                {
                    overPrice = Math.Clamp(overPrice + (decimal)Math.Round((random.NextDouble() - 0.5) * 0.12, 2), 1.70m, 2.15m);
                }
            }

            var fetched = at + span * i;
            db.OddsSnapshots.Add(Snapshot(fixture.Id, Market.AsianHandicap, homeLine, homePrice, fetched));
            db.OddsSnapshots.Add(Snapshot(fixture.Id, Market.OverUnder, totalLine, overPrice, fetched));
        }
    }

    private static OddsSnapshot Snapshot(int fixtureId, Market market, decimal line, decimal priceA, DateTime at) => new()
    {
        FixtureId = fixtureId,
        Market = market,
        Line = line,
        PriceA = priceA,
        PriceB = OtherSide(priceA),
        BookmakerId = 0,
        FetchedAtUtc = at,
        Source = OddsSource.Demo,
    };

    /// <summary>Odds cửa còn lại với lợi nhuận nhà cái ~5%.</summary>
    private static decimal OtherSide(decimal price) => Math.Round(1 / (1.05m - 1 / price), 2);

    private static void AddAdvice(AppDbContext db, Random random, Fixture fixture, double lambdaHome, double lambdaAway, DateTime now)
    {
        var diff = lambdaHome - lambdaAway + (random.NextDouble() - 0.5) * 0.4;
        var home = (int)Math.Round(Math.Clamp(45 + diff * 30, 5, 90) / 5) * 5;
        var draw = diff is > -0.3 and < 0.3 ? 40 : 25;
        var away = Math.Max(5, 100 - home - draw);
        int? winner = Math.Abs(diff) < 0.2 ? null : diff > 0 ? fixture.HomeTeamId : fixture.AwayTeamId;
        var total = lambdaHome + lambdaAway;

        db.ApiAdvices.Add(new ApiAdvice
        {
            FixtureId = fixture.Id,
            FetchedAtUtc = fixture.KickoffUtc.AddHours(-1) < now ? fixture.KickoffUtc.AddHours(-1) : now,
            WinnerTeamId = winner,
            WinnerComment = winner is null ? null : "Win or draw",
            WinOrDraw = winner is not null,
            UnderOver = total < 2.4 ? "-3.5" : total > 2.9 ? "+2.5" : "+1.5",
            Advice = winner is null ? "Combo Double chance : draw or either team" : $"Double chance : {(winner == fixture.HomeTeamId ? "home" : "away")} or draw",
            PercentHome = home,
            PercentDraw = draw,
            PercentAway = away,
            FormHome = 50 + (int)(diff * 15),
            FormAway = 50 - (int)(diff * 15),
            TotalHome = 50 + (int)(diff * 20),
            TotalAway = 50 - (int)(diff * 20),
        });
    }

    private static List<List<(int Home, int Away)>> DoubleRoundRobin(List<int> teams)
    {
        // Thuật toán vòng tròn: n-1 vòng lượt đi, đảo sân cho lượt về.
        var list = teams.ToList();
        var n = list.Count;
        var firstLeg = new List<List<(int, int)>>();
        for (var r = 0; r < n - 1; r++)
        {
            var round = new List<(int, int)>();
            for (var i = 0; i < n / 2; i++)
            {
                var a = list[i];
                var b = list[n - 1 - i];
                round.Add(r % 2 == 0 ? (a, b) : (b, a));
            }

            firstLeg.Add(round);
            list.Insert(1, list[^1]);
            list.RemoveAt(list.Count - 1);
        }

        return firstLeg.Concat(firstLeg.Select(round => round.Select(m => (m.Item2, m.Item1)).ToList())).ToList();
    }

    private static int Poisson(Random random, double lambda)
    {
        var limit = Math.Exp(-lambda);
        var k = 0;
        var p = random.NextDouble();
        while (p > limit)
        {
            k++;
            p *= random.NextDouble();
        }

        return k;
    }

    private static decimal Quarter(double value, decimal min, decimal max) =>
        Math.Clamp(Math.Round((decimal)value * 4, MidpointRounding.AwayFromZero) / 4, min, max);

    private static DateTime Round(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute >= 30 ? 30 : 0, 0, DateTimeKind.Utc);
}
