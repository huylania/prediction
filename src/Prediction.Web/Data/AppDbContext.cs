using Microsoft.EntityFrameworkCore;
using Prediction.Core.Entities;

namespace Prediction.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<League> Leagues => Set<League>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Fixture> Fixtures => Set<Fixture>();
    public DbSet<OddsSnapshot> OddsSnapshots => Set<OddsSnapshot>();
    public DbSet<ApiAdvice> ApiAdvices => Set<ApiAdvice>();
    public DbSet<Rule> Rules => Set<Rule>();
    public DbSet<Suggestion> Suggestions => Set<Suggestion>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
    public DbSet<FixtureStats> FixtureStats => Set<FixtureStats>();
    public DbSet<FixtureInjury> FixtureInjuries => Set<FixtureInjury>();
    public DbSet<TeamChange> TeamChanges => Set<TeamChange>();
    public DbSet<LeagueDna> LeagueDnas => Set<LeagueDna>();
    public DbSet<TeamDna> TeamDnas => Set<TeamDna>();
    public DbSet<TeamDnaLeague> TeamDnaLeagues => Set<TeamDnaLeague>();
    public DbSet<AnalysisPick> AnalysisPicks => Set<AnalysisPick>();
    public DbSet<LinePatternPick> LinePatternPicks => Set<LinePatternPick>();
    public DbSet<AiReport> AiReports => Set<AiReport>();
    public DbSet<CornerAiNote> CornerAiNotes => Set<CornerAiNote>();
    public DbSet<MatchSideInfoVersion> MatchSideInfoVersions => Set<MatchSideInfoVersion>();
    public DbSet<OddsCheckpoint> OddsCheckpoints => Set<OddsCheckpoint>();
    public DbSet<FixtureLineup> FixtureLineups => Set<FixtureLineup>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<FixtureWeather> FixtureWeathers => Set<FixtureWeather>();
    public DbSet<H2HMeeting> H2HMeetings => Set<H2HMeeting>();
    public DbSet<H2HCheck> H2HChecks => Set<H2HCheck>();
    public DbSet<ClosingLine> ClosingLines => Set<ClosingLine>();
    public DbSet<MatchSideInfo> MatchSideInfos => Set<MatchSideInfo>();
    public DbSet<LiveStatSnapshot> LiveStatSnapshots => Set<LiveStatSnapshot>();
    public DbSet<LiveCornerOdds> LiveCornerOdds => Set<LiveCornerOdds>();
    public DbSet<LiveCornerPick> LiveCornerPicks => Set<LiveCornerPick>();
    public DbSet<LiveOddsSnapshot> LiveOddsSnapshots => Set<LiveOddsSnapshot>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<League>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Country).HasMaxLength(100);
            e.Property(x => x.LogoUrl).HasMaxLength(500);
            e.Property(x => x.Type).HasMaxLength(20);
        });

        b.Entity<Team>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.LogoUrl).HasMaxLength(500);
            e.HasIndex(x => x.Name);
        });

        b.Entity<Fixture>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Round).HasMaxLength(100);
            e.Property(x => x.Status).HasMaxLength(10);
            e.HasOne(x => x.League).WithMany().HasForeignKey(x => x.LeagueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.HomeTeam).WithMany().HasForeignKey(x => x.HomeTeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AwayTeam).WithMany().HasForeignKey(x => x.AwayTeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.LeagueId, x.Season });
            e.HasIndex(x => x.KickoffUtc);
            e.Property(x => x.VenueName).HasMaxLength(200);
        });

        b.Entity<OddsSnapshot>(e =>
        {
            e.Property(x => x.Line).HasPrecision(6, 2);
            e.Property(x => x.PriceA).HasPrecision(8, 3);
            e.Property(x => x.PriceB).HasPrecision(8, 3);
            e.HasOne(x => x.Fixture).WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FixtureId, x.Market, x.FetchedAtUtc });
        });

        b.Entity<ApiAdvice>(e =>
        {
            e.Property(x => x.WinnerComment).HasMaxLength(200);
            e.Property(x => x.UnderOver).HasMaxLength(20);
            e.Property(x => x.Advice).HasMaxLength(500);
            e.HasOne(x => x.Fixture).WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.FixtureId).IsUnique();
        });

        b.Entity<Rule>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(2000);
            foreach (var p in new[] { nameof(Rule.AhLineMin), nameof(Rule.AhLineMax), nameof(Rule.OuLineMin), nameof(Rule.OuLineMax), nameof(Rule.MinAhLineMove), nameof(Rule.MinOuLineMove) })
            {
                e.Property(p).HasPrecision(6, 2);
            }

            foreach (var p in new[] { nameof(Rule.MinOdds), nameof(Rule.MinPriceDrop), nameof(Rule.MinAvgTotalGoals), nameof(Rule.MaxAvgTotalGoals) })
            {
                e.Property(p).HasPrecision(8, 3);
            }

            e.HasOne(x => x.League).WithMany().HasForeignKey(x => x.LeagueId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.SubjectTeam).WithMany().HasForeignKey(x => x.SubjectTeamId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Suggestion>(e =>
        {
            e.Property(x => x.FirstLine).HasPrecision(6, 2);
            e.Property(x => x.Line).HasPrecision(6, 2);
            e.Property(x => x.FirstOdds).HasPrecision(8, 3);
            e.Property(x => x.Odds).HasPrecision(8, 3);
            e.Property(x => x.Profit).HasPrecision(10, 4);
            e.HasOne(x => x.Rule).WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Fixture).WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RuleId, x.FixtureId, x.Side }).IsUnique();
        });

        b.Entity<AdminUser>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(100);
            e.HasIndex(x => x.Username).IsUnique();
        });

        b.Entity<SyncLog>(e =>
        {
            e.Property(x => x.Job).HasMaxLength(50);
            e.Property(x => x.Message).HasMaxLength(2000);
            e.HasIndex(x => x.StartedAtUtc);
        });

        b.Entity<FixtureStats>(e =>
        {
            e.HasKey(x => x.FixtureId);
            e.HasOne(x => x.Fixture).WithOne().HasForeignKey<FixtureStats>(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.HomeXg).HasPrecision(6, 2);
            e.Property(x => x.AwayXg).HasPrecision(6, 2);
            e.Property(x => x.GoalTimeline).HasMaxLength(500);
            e.HasIndex(x => x.IsFinal);
        });

        b.Entity<FixtureInjury>(e =>
        {
            e.Property(x => x.PlayerName).HasMaxLength(150);
            e.Property(x => x.Type).HasMaxLength(50);
            e.Property(x => x.Reason).HasMaxLength(200);
            e.HasOne(x => x.Fixture).WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.FixtureId);
        });

        b.Entity<TeamChange>(e =>
        {
            e.Property(x => x.PersonName).HasMaxLength(150);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeamId, x.Kind });
        });

        b.Entity<LeagueDna>(e =>
        {
            e.HasKey(x => x.LeagueId);
            e.Property(x => x.LeagueId).ValueGeneratedNever();
            e.Property(x => x.LeagueName).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.Country).HasMaxLength(100);
            e.Property(x => x.Summary).HasMaxLength(500);
            e.Property(x => x.Analysis).HasMaxLength(2000);
            foreach (var p in new[] { nameof(LeagueDna.AvgGoals), nameof(LeagueDna.AvgCorners), nameof(LeagueDna.AvgCards) })
            {
                e.Property<decimal?>(p).HasPrecision(5, 2);
            }

            foreach (var p in new[] { nameof(LeagueDna.Over25Pct), nameof(LeagueDna.DrawPct), nameof(LeagueDna.HomeWinPct), nameof(LeagueDna.FirstHalfGoalPct), nameof(LeagueDna.LateGoalPct) })
            {
                e.Property<decimal?>(p).HasPrecision(5, 1);
            }
        });

        b.Entity<TeamDna>(e =>
        {
            e.HasKey(x => x.TeamId);
            e.Property(x => x.TeamId).ValueGeneratedNever();
            e.Property(x => x.TeamName).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.SourceLeagueName).HasMaxLength(100);
            e.Property(x => x.Summary).HasMaxLength(500);
            e.Property(x => x.Analysis).HasMaxLength(2000);
            foreach (var p in new[] { nameof(TeamDna.AvgTotalGoals), nameof(TeamDna.AvgGoalsFor), nameof(TeamDna.AvgGoalsAgainst), nameof(TeamDna.AvgCornersFor),
                         nameof(TeamDna.AvgCornersAgainst), nameof(TeamDna.AvgSecondHalfGoals), nameof(TeamDna.AvgCards) })
            {
                e.Property<decimal?>(p).HasPrecision(5, 2);
            }

            foreach (var p in new[] { nameof(TeamDna.Over25Pct), nameof(TeamDna.BttsPct), nameof(TeamDna.HomeWinPct), nameof(TeamDna.AwayLossPct), nameof(TeamDna.FirstHalfGoalPct) })
            {
                e.Property<decimal?>(p).HasPrecision(5, 1);
            }
        });

        b.Entity<TeamDnaLeague>(e =>
        {
            e.HasKey(x => new { x.TeamId, x.LeagueId });
            e.Property(x => x.LeagueName).HasMaxLength(100);
            e.HasIndex(x => x.LeagueId);
            e.HasOne<TeamDna>().WithMany(d => d.Leagues).HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AnalysisPick>(e =>
        {
            e.Property(x => x.Line).HasPrecision(6, 2);
            e.Property(x => x.Odds).HasPrecision(8, 3);
            e.Property(x => x.Score).HasPrecision(4, 1);
            e.Property(x => x.WinProbability).HasPrecision(5, 4);
            e.Property(x => x.ExpectedValue).HasPrecision(8, 4);
            e.Property(x => x.Profit).HasPrecision(10, 4);
            e.Property(x => x.Label).HasMaxLength(200);
            e.Property(x => x.Flags).HasMaxLength(50);
            e.HasOne(x => x.Fixture).WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FixtureId, x.Market }).IsUnique();
            e.HasIndex(x => x.Outcome);
        });

        b.Entity<LinePatternPick>(e =>
        {
            e.Property(x => x.Line).HasPrecision(6, 2);
            e.Property(x => x.Odds).HasPrecision(8, 3);
            e.Property(x => x.Score).HasPrecision(4, 1);
            e.Property(x => x.WinProbability).HasPrecision(5, 4);
            e.Property(x => x.PushProbability).HasPrecision(5, 4);
            e.Property(x => x.ExpectedValue).HasPrecision(8, 4);
            e.Property(x => x.MinOdds).HasPrecision(8, 3);
            e.Property(x => x.Profit).HasPrecision(10, 4);
            e.Property(x => x.Label).HasMaxLength(200);
            e.Property(x => x.EntryText).HasMaxLength(400);
            e.Property(x => x.Indicators).HasMaxLength(1000);
            e.HasOne(x => x.Fixture).WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FixtureId, x.Pattern }).IsUnique();
            e.HasIndex(x => x.Outcome);
        });

        b.Entity<AiReport>(e =>
        {
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.StopReason).HasMaxLength(50);
            e.Property(x => x.CostUsd).HasPrecision(10, 4);
            e.HasOne(x => x.Fixture).WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FixtureId, x.CreatedAtUtc });
        });

        b.Entity<OddsCheckpoint>(e =>
        {
            foreach (var p in new[] { nameof(OddsCheckpoint.HandicapLine), nameof(OddsCheckpoint.TotalLine) })
            {
                e.Property(p).HasPrecision(6, 2);
            }

            foreach (var p in new[] { nameof(OddsCheckpoint.HandicapHome), nameof(OddsCheckpoint.HandicapAway), nameof(OddsCheckpoint.Over), nameof(OddsCheckpoint.Under) })
            {
                e.Property(p).HasPrecision(8, 3);
            }

            e.HasOne(x => x.Fixture).WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FixtureId, x.Kind }).IsUnique();
        });

        b.Entity<FixtureLineup>(e =>
        {
            e.HasKey(x => x.FixtureId);
            e.HasOne(x => x.Fixture).WithOne().HasForeignKey<FixtureLineup>(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.HomeFormation).HasMaxLength(20);
            e.Property(x => x.AwayFormation).HasMaxLength(20);
            e.Property(x => x.HomeStartXI).HasMaxLength(1000);
            e.Property(x => x.AwayStartXI).HasMaxLength(1000);
            e.Property(x => x.HomeCoach).HasMaxLength(150);
            e.Property(x => x.AwayCoach).HasMaxLength(150);
        });

        b.Entity<Venue>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.City).HasMaxLength(150);
            e.Property(x => x.Country).HasMaxLength(100);
            e.Property(x => x.Surface).HasMaxLength(50);
            e.Property(x => x.GeoSource).HasMaxLength(30);
        });

        b.Entity<FixtureWeather>(e =>
        {
            e.HasKey(x => x.FixtureId);
            e.HasOne(x => x.Fixture).WithOne().HasForeignKey<FixtureWeather>(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Source).HasMaxLength(30);
        });

        b.Entity<H2HMeeting>(e =>
        {
            e.HasKey(x => x.FixtureId);
            e.Property(x => x.FixtureId).ValueGeneratedNever();
            e.Property(x => x.PairKey).HasMaxLength(30);
            e.Property(x => x.LeagueName).HasMaxLength(200);
            e.HasIndex(x => x.PairKey);
        });

        b.Entity<H2HCheck>(e =>
        {
            e.HasKey(x => x.PairKey);
            e.Property(x => x.PairKey).HasMaxLength(30);
        });

        b.Entity<ClosingLine>(e =>
        {
            e.HasKey(x => x.FixtureId);
            e.Property(x => x.FixtureId).ValueGeneratedNever();
            e.Property(x => x.HandicapLine).HasPrecision(6, 2);
            e.Property(x => x.TotalLine).HasPrecision(6, 2);
            e.Property(x => x.Note).HasMaxLength(500);
        });

        b.Entity<MatchSideInfo>(e =>
        {
            e.HasKey(x => x.FixtureId);
            e.HasOne(x => x.Fixture).WithOne().HasForeignKey<MatchSideInfo>(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Rivalry).HasMaxLength(2000);
            e.Property(x => x.Motivation).HasMaxLength(2000);
            e.Property(x => x.News).HasMaxLength(3000);
            e.Property(x => x.VenueNotes).HasMaxLength(1000);
            e.Property(x => x.Weather).HasMaxLength(500);
            e.Property(x => x.Surface).HasMaxLength(50);
            e.Property(x => x.Sources).HasMaxLength(3000);
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.CostUsd).HasPrecision(10, 4);
            e.HasIndex(x => x.CreatedAtUtc);
        });

        b.Entity<MatchSideInfoVersion>(e =>
        {
            e.HasOne(x => x.Fixture).WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Rivalry).HasMaxLength(2000);
            e.Property(x => x.Motivation).HasMaxLength(2000);
            e.Property(x => x.News).HasMaxLength(3000);
            e.Property(x => x.VenueNotes).HasMaxLength(1000);
            e.Property(x => x.Weather).HasMaxLength(500);
            e.Property(x => x.Surface).HasMaxLength(50);
            e.Property(x => x.Sources).HasMaxLength(3000);
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.CostUsd).HasPrecision(10, 4);
            e.HasIndex(x => new { x.FixtureId, x.ReplacedAtUtc });
        });

        b.Entity<CornerAiNote>(e =>
        {
            e.Property(x => x.Match).HasMaxLength(200);
            e.Property(x => x.Headline).HasMaxLength(300);
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.CostUsd).HasPrecision(10, 4);
            e.Property(x => x.Line).HasPrecision(6, 2);
            e.Property(x => x.OverOdds).HasPrecision(6, 2);
            e.Property(x => x.UnderOdds).HasPrecision(6, 2);
            e.Ignore(x => x.TotalCorners);
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasIndex(x => new { x.FixtureId, x.Window, x.CreatedAtUtc });
        });

        b.Entity<LiveStatSnapshot>(e =>
        {
            e.Property(x => x.Status).HasMaxLength(10);
            e.HasOne(x => x.Fixture).WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FixtureId, x.Minute });
        });

        b.Entity<LiveCornerOdds>(e =>
        {
            e.Property(x => x.Line).HasPrecision(6, 2);
            e.Property(x => x.OverOdds).HasPrecision(8, 3);
            e.Property(x => x.UnderOdds).HasPrecision(8, 3);
            e.HasOne(x => x.Fixture).WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FixtureId, x.Window, x.FetchedAtUtc });
        });

        b.Entity<LiveOddsSnapshot>(e =>
        {
            e.Property(x => x.Line).HasPrecision(6, 2);
            e.Property(x => x.PriceA).HasPrecision(8, 3);
            e.Property(x => x.PriceB).HasPrecision(8, 3);
            e.HasOne(x => x.Fixture).WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FixtureId, x.Market, x.FirstHalf, x.FetchedAtUtc });
        });

        b.Entity<AiUsageLog>(e =>
        {
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.Subject).HasMaxLength(200);
            e.Property(x => x.Error).HasMaxLength(500);
            e.Property(x => x.CostUsd).HasPrecision(10, 4);
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasIndex(x => new { x.Feature, x.CreatedAtUtc });
        });

        b.Entity<AppSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).HasMaxLength(1000);
        });

        b.Entity<LiveCornerPick>(e =>
        {
            e.Property(x => x.Line).HasPrecision(6, 2);
            e.Property(x => x.Odds).HasPrecision(8, 3);
            e.Property(x => x.ExpectedTotal).HasPrecision(6, 2);
            e.Property(x => x.Edge).HasPrecision(6, 2);
            e.Property(x => x.WinProbability).HasPrecision(5, 4);
            e.Property(x => x.Confidence).HasPrecision(4, 1);
            e.Property(x => x.Profit).HasPrecision(10, 4);
            e.HasOne(x => x.Fixture).WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FixtureId, x.Window }).IsUnique();
        });

        SeedData.Apply(b);
        DnaSeed.Apply(b);
    }
}
