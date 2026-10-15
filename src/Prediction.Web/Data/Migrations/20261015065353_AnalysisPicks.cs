using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AnalysisPicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisPicks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    Market = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    Line = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Odds = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    Score = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    Group = table.Column<int>(type: "int", nullable: false),
                    WinProbability = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    ExpectedValue = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Flags = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Profit = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisPicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisPicks_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisPicks_FixtureId_Market",
                table: "AnalysisPicks",
                columns: new[] { "FixtureId", "Market" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisPicks_Outcome",
                table: "AnalysisPicks",
                column: "Outcome");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisPicks");
        }
    }
}
