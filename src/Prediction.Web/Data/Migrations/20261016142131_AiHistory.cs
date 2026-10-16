using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AiHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Complete",
                table: "AiReports",
                type: "bit",
                nullable: false,
                defaultValue: true); // báo cáo đã lưu trước đây đều là bản hoàn chỉnh

            migrationBuilder.CreateTable(
                name: "CornerAiNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    FixtureId = table.Column<int>(type: "int", nullable: true),
                    Match = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Window = table.Column<int>(type: "int", nullable: false),
                    Minute = table.Column<int>(type: "int", nullable: false),
                    HomeCorners = table.Column<int>(type: "int", nullable: false),
                    AwayCorners = table.Column<int>(type: "int", nullable: false),
                    HomeGoals = table.Column<int>(type: "int", nullable: false),
                    AwayGoals = table.Column<int>(type: "int", nullable: false),
                    Line = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    OverOdds = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    UnderOdds = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    ExpectedTotal = table.Column<double>(type: "float", nullable: true),
                    Headline = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Markdown = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CostUsd = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Complete = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CornerAiNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchSideInfoVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    Rivalry = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Motivation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    News = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    VenueNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Weather = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AltitudeM = table.Column<int>(type: "int", nullable: true),
                    Surface = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Sources = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CostUsd = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    IsAuto = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReplacedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSideInfoVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchSideInfoVersions_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CornerAiNotes_CreatedAtUtc",
                table: "CornerAiNotes",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CornerAiNotes_FixtureId_Window_CreatedAtUtc",
                table: "CornerAiNotes",
                columns: new[] { "FixtureId", "Window", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchSideInfoVersions_FixtureId_ReplacedAtUtc",
                table: "MatchSideInfoVersions",
                columns: new[] { "FixtureId", "ReplacedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CornerAiNotes");

            migrationBuilder.DropTable(
                name: "MatchSideInfoVersions");

            migrationBuilder.DropColumn(
                name: "Complete",
                table: "AiReports");
        }
    }
}
