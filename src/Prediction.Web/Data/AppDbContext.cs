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
        });

        b.Entity<TeamDna>(e =>
        {
            e.HasKey(x => x.TeamId);
            e.Property(x => x.TeamId).ValueGeneratedNever();
            e.Property(x => x.TeamName).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(1000);
        });

        SeedData.Apply(b);
        DnaSeed.Apply(b);
    }
}
