using Microsoft.EntityFrameworkCore;
using Prediction.Core.Entities;

namespace Prediction.Web.Data;

public static class SeedData
{
    public const int ManchesterUnitedId = 33;

    public static void Apply(ModelBuilder b)
    {
        // Id theo API-Football. Tên/mùa/logo được cập nhật lại ở lần đồng bộ "giải đấu" đầu tiên.
        b.Entity<League>().HasData(
            new League { Id = 39, Name = "Premier League", Country = "England", Type = "League", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 140, Name = "La Liga", Country = "Spain", Type = "League", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 135, Name = "Serie A", Country = "Italy", Type = "League", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 78, Name = "Bundesliga", Country = "Germany", Type = "League", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 61, Name = "Ligue 1", Country = "France", Type = "League", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 2, Name = "UEFA Champions League", Country = "World", Type = "Cup", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 340, Name = "V.League 1", Country = "Vietnam", Type = "League", CurrentSeason = 2027, SeasonYearIsEndYear = true, IsTracked = true });

        b.Entity<Team>().HasData(new Team { Id = ManchesterUnitedId, Name = "Manchester United" });

        b.Entity<Rule>().HasData(new Rule
        {
            Id = 1,
            Name = "MU gặp đội xếp dưới → chọn MU",
            Notes = "Ví dụ từ kinh nghiệm: MU gặp đội thấp hơn trên BXH thì chọn MU (kèo chấp). Sửa điều kiện mức chấp, phong độ... cho đúng thói quen của bạn rồi chạy Kiểm chứng.",
            IsActive = true,
            CreatedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            LeagueId = 39,
            Market = Market.AsianHandicap,
            Selection = RuleSelection.Subject,
            SubjectTeamId = ManchesterUnitedId,
            MinRankGap = 1,
            MinMatchesPlayed = 5,
        });
    }
}
