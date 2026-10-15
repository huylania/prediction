using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class LiveCorners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayBlocked",
                table: "FixtureStats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayShotsInBox",
                table: "FixtureStats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirstHalfAwayCorners",
                table: "FixtureStats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirstHalfHomeCorners",
                table: "FixtureStats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeBlocked",
                table: "FixtureStats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeShotsInBox",
                table: "FixtureStats",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LiveCornerOdds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    Window = table.Column<int>(type: "int", nullable: false),
                    Line = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    OverOdds = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    UnderOdds = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    Minute = table.Column<int>(type: "int", nullable: true),
                    FetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveCornerOdds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveCornerOdds_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiveCornerPicks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    Window = table.Column<int>(type: "int", nullable: false),
                    Minute = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    Line = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Odds = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    CornersAtPick = table.Column<int>(type: "int", nullable: false),
                    ExpectedTotal = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Edge = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WinProbability = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinalCorners = table.Column<int>(type: "int", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Profit = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveCornerPicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveCornerPicks_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiveStatSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    Minute = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HomeGoals = table.Column<int>(type: "int", nullable: false),
                    AwayGoals = table.Column<int>(type: "int", nullable: false),
                    HomeCorners = table.Column<int>(type: "int", nullable: false),
                    AwayCorners = table.Column<int>(type: "int", nullable: false),
                    HomeShots = table.Column<int>(type: "int", nullable: true),
                    AwayShots = table.Column<int>(type: "int", nullable: true),
                    HomeOnTarget = table.Column<int>(type: "int", nullable: true),
                    AwayOnTarget = table.Column<int>(type: "int", nullable: true),
                    HomeBlocked = table.Column<int>(type: "int", nullable: true),
                    AwayBlocked = table.Column<int>(type: "int", nullable: true),
                    HomeRed = table.Column<int>(type: "int", nullable: false),
                    AwayRed = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveStatSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveStatSnapshots_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiveCornerOdds_FixtureId_Window_FetchedAtUtc",
                table: "LiveCornerOdds",
                columns: new[] { "FixtureId", "Window", "FetchedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LiveCornerPicks_FixtureId_Window",
                table: "LiveCornerPicks",
                columns: new[] { "FixtureId", "Window" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiveStatSnapshots_FixtureId_Minute",
                table: "LiveStatSnapshots",
                columns: new[] { "FixtureId", "Minute" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiveCornerOdds");

            migrationBuilder.DropTable(
                name: "LiveCornerPicks");

            migrationBuilder.DropTable(
                name: "LiveStatSnapshots");

            migrationBuilder.DropColumn(
                name: "AwayBlocked",
                table: "FixtureStats");

            migrationBuilder.DropColumn(
                name: "AwayShotsInBox",
                table: "FixtureStats");

            migrationBuilder.DropColumn(
                name: "FirstHalfAwayCorners",
                table: "FixtureStats");

            migrationBuilder.DropColumn(
                name: "FirstHalfHomeCorners",
                table: "FixtureStats");

            migrationBuilder.DropColumn(
                name: "HomeBlocked",
                table: "FixtureStats");

            migrationBuilder.DropColumn(
                name: "HomeShotsInBox",
                table: "FixtureStats");
        }
    }
}
