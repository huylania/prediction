using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AiReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Markdown = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StopReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    CacheReadTokens = table.Column<int>(type: "int", nullable: false),
                    CacheWriteTokens = table.Column<int>(type: "int", nullable: false),
                    WebSearches = table.Column<int>(type: "int", nullable: false),
                    CostUsd = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiReports_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiReports_FixtureId_CreatedAtUtc",
                table: "AiReports",
                columns: new[] { "FixtureId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiReports");
        }
    }
}
