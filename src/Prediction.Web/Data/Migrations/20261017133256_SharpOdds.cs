using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SharpOdds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SharpOdds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    BookmakerId = table.Column<int>(type: "int", nullable: false),
                    Market = table.Column<int>(type: "int", nullable: false),
                    Line = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    PriceA = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    PriceB = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    IsMain = table.Column<bool>(type: "bit", nullable: false),
                    IsOpening = table.Column<bool>(type: "bit", nullable: false),
                    BookmakerUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharpOdds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharpOdds_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SharpOdds_FixtureId_Market",
                table: "SharpOdds",
                columns: new[] { "FixtureId", "Market" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SharpOdds");
        }
    }
}
