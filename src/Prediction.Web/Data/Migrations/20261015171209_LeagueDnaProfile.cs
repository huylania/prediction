using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class LeagueDnaProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Analysis",
                table: "LeagueDnas",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AvgCards",
                table: "LeagueDnas",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AvgCorners",
                table: "LeagueDnas",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AvgGoals",
                table: "LeagueDnas",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "LeagueDnas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DrawPct",
                table: "LeagueDnas",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FirstHalfGoalPct",
                table: "LeagueDnas",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HomeWinPct",
                table: "LeagueDnas",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LateGoalPct",
                table: "LeagueDnas",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Over25Pct",
                table: "LeagueDnas",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProfileUpdatedAtUtc",
                table: "LeagueDnas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "LeagueDnas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 39,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 40,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 62,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 71,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 72,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 78,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 79,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 88,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 89,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 94,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 103,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 119,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 128,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 135,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 141,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 179,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 188,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 197,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 203,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 207,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 218,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 233,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 239,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 242,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 253,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 281,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 290,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LeagueDnas",
                keyColumn: "LeagueId",
                keyValue: 344,
                columns: new[] { "Analysis", "AvgCards", "AvgCorners", "AvgGoals", "Country", "DrawPct", "FirstHalfGoalPct", "HomeWinPct", "LateGoalPct", "Over25Pct", "ProfileUpdatedAtUtc", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null });

            // Các giải có trong file DNA giải (46 giải) mà website chưa có: thêm và bật theo dõi (giải đã có nhưng đang tắt thì bật lại).
            // Mùa hiện tại, kiểu mùa và độ phủ thống kê lấy từ API ngày 15/10/2026; tác vụ "Giải đấu" vẫn tự cập nhật hằng ngày.
            foreach (var (id, country, name, type, season, calendar, stats, injuries) in new[]
            {
                (3, "World", "UEFA Europa League", "Cup", 2026, false, true, true),
                (848, "World", "UEFA Europa Conference League", "Cup", 2026, true, true, false),
                (40, "England", "Championship", "League", 2026, false, true, true),
                (203, "Turkey", "Süper Lig", "League", 2026, false, true, true),
                (204, "Turkey", "1. Lig", "League", 2026, false, true, false),
                (235, "Russia", "Premier League", "League", 2026, false, true, true),
                (333, "Ukraine", "Premier League", "League", 2026, false, true, false),
                (129, "Argentina", "Primera Nacional", "League", 2026, true, false, false),
                (278, "Malaysia", "Super League", "League", 2026, false, false, false),
                (100, "Japan", "J3 League", "League", 2026, false, false, false),
                (295, "South-Korea", "K3 League", "League", 2026, true, false, false),
                (1234, "South-Korea", "K4 League", "League", 2026, true, false, false),
                (308, "Saudi-Arabia", "Division 1", "League", 2026, false, false, false),
                (13, "World", "CONMEBOL Libertadores", "Cup", 2026, true, true, false),
                (11, "World", "CONMEBOL Sudamericana", "Cup", 2026, true, true, false),
                (9, "World", "Copa America", "Cup", 2024, true, true, false),
            })
            {
                migrationBuilder.Sql(
                    $"IF NOT EXISTS (SELECT 1 FROM [Leagues] WHERE [Id] = {id}) " +
                    "INSERT INTO [Leagues] ([Id], [Country], [CurrentSeason], [HasInjuriesCoverage], [HasStatsCoverage], [IsDemo], [IsTracked], [LogoUrl], [Name], [SeasonIsCalendarYear], [SeasonYearIsEndYear], [Type]) " +
                    $"VALUES ({id}, N'{country}', {season}, {(injuries ? 1 : 0)}, {(stats ? 1 : 0)}, 0, 1, N'https://media.api-sports.io/football/leagues/{id}.png', N'{name}', {(calendar ? 1 : 0)}, 0, N'{type}') " +
                    $"ELSE UPDATE [Leagues] SET [IsTracked] = 1 WHERE [Id] = {id} AND [IsDemo] = 0;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Analysis",
                table: "LeagueDnas");

            migrationBuilder.DropColumn(
                name: "AvgCards",
                table: "LeagueDnas");

            migrationBuilder.DropColumn(
                name: "AvgCorners",
                table: "LeagueDnas");

            migrationBuilder.DropColumn(
                name: "AvgGoals",
                table: "LeagueDnas");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "LeagueDnas");

            migrationBuilder.DropColumn(
                name: "DrawPct",
                table: "LeagueDnas");

            migrationBuilder.DropColumn(
                name: "FirstHalfGoalPct",
                table: "LeagueDnas");

            migrationBuilder.DropColumn(
                name: "HomeWinPct",
                table: "LeagueDnas");

            migrationBuilder.DropColumn(
                name: "LateGoalPct",
                table: "LeagueDnas");

            migrationBuilder.DropColumn(
                name: "Over25Pct",
                table: "LeagueDnas");

            migrationBuilder.DropColumn(
                name: "ProfileUpdatedAtUtc",
                table: "LeagueDnas");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "LeagueDnas");
        }
    }
}
