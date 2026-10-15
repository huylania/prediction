using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class DnaLeagues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamDnaLeagues",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    LeagueName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Matches = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamDnaLeagues", x => new { x.TeamId, x.LeagueId });
                    table.ForeignKey(
                        name: "FK_TeamDnaLeagues_TeamDnas_TeamId",
                        column: x => x.TeamId,
                        principalTable: "TeamDnas",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamDnaLeagues_LeagueId",
                table: "TeamDnaLeagues",
                column: "LeagueId");

            // Đội đã nạp hồ sơ 1 năm từ script trước: thuộc ít nhất giải lấy số liệu.
            migrationBuilder.Sql(
                "INSERT INTO [TeamDnaLeagues] ([TeamId], [LeagueId], [LeagueName], [Matches]) " +
                "SELECT [TeamId], [SourceLeagueId], ISNULL([SourceLeagueName], N''), [Matches] FROM [TeamDnas] WHERE [SourceLeagueId] IS NOT NULL;");

            // Các giải có trong file DNA 30 giải mà website chưa có sẵn: thêm và bật theo dõi (giải đã có nhưng đang tắt thì bật lại).
            // Mùa hiện tại, kiểu mùa và độ phủ thống kê lấy từ API ngày 15/10/2026; tác vụ "Giải đấu" vẫn tự cập nhật hằng ngày.
            foreach (var (id, country, name, season, endYear, calendar, stats) in new[]
            {
                (98, "Japan", "J1 League", 2027, true, false, true),
                (99, "Japan", "J2 League", 2026, false, false, false),
                (162, "Costa-Rica", "Primera División", 2026, false, true, false),
                (239, "Colombia", "Primera A", 2026, false, true, true),
                (240, "Colombia", "Primera B", 2026, false, true, false),
                (262, "Mexico", "Liga MX", 2026, false, true, true),
                (263, "Mexico", "Liga de Expansión MX", 2026, false, true, false),
                (274, "Indonesia", "Liga 1", 2026, false, false, false),
                (292, "South-Korea", "K League 1", 2026, false, true, true),
                (293, "South-Korea", "K League 2", 2026, false, true, false),
                (307, "Saudi-Arabia", "Pro League", 2026, false, false, true),
            })
            {
                migrationBuilder.Sql(
                    $"IF NOT EXISTS (SELECT 1 FROM [Leagues] WHERE [Id] = {id}) " +
                    "INSERT INTO [Leagues] ([Id], [Country], [CurrentSeason], [HasInjuriesCoverage], [HasStatsCoverage], [IsDemo], [IsTracked], [LogoUrl], [Name], [SeasonIsCalendarYear], [SeasonYearIsEndYear], [Type]) " +
                    $"VALUES ({id}, N'{country}', {season}, 0, {(stats ? 1 : 0)}, 0, 1, N'https://media.api-sports.io/football/leagues/{id}.png', N'{name}', {(calendar ? 1 : 0)}, {(endYear ? 1 : 0)}, N'League') " +
                    $"ELSE UPDATE [Leagues] SET [IsTracked] = 1 WHERE [Id] = {id} AND [IsDemo] = 0;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamDnaLeagues");
        }
    }
}
