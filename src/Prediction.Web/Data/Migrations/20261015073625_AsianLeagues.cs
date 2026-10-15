using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AsianLeagues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Giải có thể đã được thêm tay trong Cài đặt: chỉ chèn giải chưa có để migration không lỗi trùng khóa.
            foreach (var (id, country, name, type, stats) in new[]
            {
                (17, "World", "AFC Champions League Elite", "Cup", true),
                (18, "World", "AFC Champions League Two", "Cup", true),
                (296, "Thailand", "Thai League 1", "League", true),
                (297, "Thailand", "Thai League 2", "League", false),
            })
            {
                migrationBuilder.Sql(
                    $"IF NOT EXISTS (SELECT 1 FROM [Leagues] WHERE [Id] = {id}) " +
                    "INSERT INTO [Leagues] ([Id], [Country], [CurrentSeason], [HasInjuriesCoverage], [HasStatsCoverage], [IsDemo], [IsTracked], [LogoUrl], [Name], [SeasonIsCalendarYear], [SeasonYearIsEndYear], [Type]) " +
                    $"VALUES ({id}, N'{country}', 2026, {(stats ? 1 : 0)}, {(stats ? 1 : 0)}, 0, 1, NULL, N'{name}', 0, 0, N'{type}');");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 296);

            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 297);
        }
    }
}
