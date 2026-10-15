using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AiUsageLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUsageLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Feature = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FixtureId = table.Column<int>(type: "int", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    CacheReadTokens = table.Column<int>(type: "int", nullable: false),
                    CacheWriteTokens = table.Column<int>(type: "int", nullable: false),
                    WebSearches = table.Column<int>(type: "int", nullable: false),
                    CostUsd = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    Ok = table.Column<bool>(type: "bit", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLogs_CreatedAtUtc",
                table: "AiUsageLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLogs_Feature_CreatedAtUtc",
                table: "AiUsageLogs",
                columns: new[] { "Feature", "CreatedAtUtc" });

            // Nhập chi phí đã có từ trước khi có log: báo cáo AI đã lưu và thông tin bên lề (mỗi trận chỉ còn bản mới nhất).
            migrationBuilder.Sql("""
                INSERT INTO [AiUsageLogs] ([CreatedAtUtc], [Feature], [Model], [FixtureId], [Subject], [InputTokens], [OutputTokens],
                    [CacheReadTokens], [CacheWriteTokens], [WebSearches], [CostUsd], [DurationMs], [Ok], [Error])
                SELECT r.[CreatedAtUtc], 1, LEFT(r.[Model], 100), r.[FixtureId],
                    LEFT(COALESCE(h.[Name] + N' – ' + a.[Name], N'Trận #' + CAST(r.[FixtureId] AS nvarchar(20))) + N' (dữ liệu cũ)', 200),
                    r.[InputTokens], r.[OutputTokens], r.[CacheReadTokens], r.[CacheWriteTokens], r.[WebSearches], r.[CostUsd], 0, 1, NULL
                FROM [AiReports] r
                LEFT JOIN [Fixtures] f ON f.[Id] = r.[FixtureId]
                LEFT JOIN [Teams] h ON h.[Id] = f.[HomeTeamId]
                LEFT JOIN [Teams] a ON a.[Id] = f.[AwayTeamId];

                INSERT INTO [AiUsageLogs] ([CreatedAtUtc], [Feature], [Model], [FixtureId], [Subject], [InputTokens], [OutputTokens],
                    [CacheReadTokens], [CacheWriteTokens], [WebSearches], [CostUsd], [DurationMs], [Ok], [Error])
                SELECT s.[CreatedAtUtc], CASE WHEN s.[IsAuto] = 1 THEN 2 ELSE 3 END, LEFT(s.[Model], 100), s.[FixtureId],
                    LEFT(COALESCE(h.[Name] + N' – ' + a.[Name], N'Trận #' + CAST(s.[FixtureId] AS nvarchar(20))) + N' (dữ liệu cũ)', 200),
                    0, 0, 0, 0, 0, s.[CostUsd], 0, 1, NULL
                FROM [MatchSideInfos] s
                LEFT JOIN [Fixtures] f ON f.[Id] = s.[FixtureId]
                LEFT JOIN [Teams] h ON h.[Id] = f.[HomeTeamId]
                LEFT JOIN [Teams] a ON a.[Id] = f.[AwayTeamId]
                WHERE s.[CostUsd] > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageLogs");

            migrationBuilder.DropTable(
                name: "AppSettings");
        }
    }
}
