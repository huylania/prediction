using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class MatchContextData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VenueId",
                table: "Fixtures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VenueName",
                table: "Fixtures",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClosingLines",
                columns: table => new
                {
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    HandicapLine = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    TotalLine = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosingLines", x => x.FixtureId);
                });

            migrationBuilder.CreateTable(
                name: "FixtureLineups",
                columns: table => new
                {
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HomeFormation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AwayFormation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HomeStartXI = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AwayStartXI = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    HomeCoach = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AwayCoach = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixtureLineups", x => x.FixtureId);
                    table.ForeignKey(
                        name: "FK_FixtureLineups_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FixtureWeathers",
                columns: table => new
                {
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    TemperatureC = table.Column<double>(type: "float", nullable: true),
                    HumidityPercent = table.Column<int>(type: "int", nullable: true),
                    PrecipitationMm = table.Column<double>(type: "float", nullable: true),
                    PrecipitationProbability = table.Column<int>(type: "int", nullable: true),
                    WindKph = table.Column<double>(type: "float", nullable: true),
                    WeatherCode = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixtureWeathers", x => x.FixtureId);
                    table.ForeignKey(
                        name: "FK_FixtureWeathers_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "H2HChecks",
                columns: table => new
                {
                    PairKey = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_H2HChecks", x => x.PairKey);
                });

            migrationBuilder.CreateTable(
                name: "H2HMeetings",
                columns: table => new
                {
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    PairKey = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    KickoffUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeagueName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HomeTeamId = table.Column<int>(type: "int", nullable: false),
                    AwayTeamId = table.Column<int>(type: "int", nullable: false),
                    HomeGoals = table.Column<int>(type: "int", nullable: true),
                    AwayGoals = table.Column<int>(type: "int", nullable: true),
                    HalftimeHome = table.Column<int>(type: "int", nullable: true),
                    HalftimeAway = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_H2HMeetings", x => x.FixtureId);
                });

            migrationBuilder.CreateTable(
                name: "MatchSideInfos",
                columns: table => new
                {
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
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSideInfos", x => x.FixtureId);
                    table.ForeignKey(
                        name: "FK_MatchSideInfos_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OddsCheckpoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BookmakerUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandicapLine = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    HandicapHome = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: true),
                    HandicapAway = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: true),
                    TotalLine = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Over = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: true),
                    Under = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OddsCheckpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OddsCheckpoints_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    Surface = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    ElevationM = table.Column<int>(type: "int", nullable: true),
                    GeoSource = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    GeoCheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_H2HMeetings_PairKey",
                table: "H2HMeetings",
                column: "PairKey");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSideInfos_CreatedAtUtc",
                table: "MatchSideInfos",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OddsCheckpoints_FixtureId_Kind",
                table: "OddsCheckpoints",
                columns: new[] { "FixtureId", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClosingLines");

            migrationBuilder.DropTable(
                name: "FixtureLineups");

            migrationBuilder.DropTable(
                name: "FixtureWeathers");

            migrationBuilder.DropTable(
                name: "H2HChecks");

            migrationBuilder.DropTable(
                name: "H2HMeetings");

            migrationBuilder.DropTable(
                name: "MatchSideInfos");

            migrationBuilder.DropTable(
                name: "OddsCheckpoints");

            migrationBuilder.DropTable(
                name: "Venues");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "Fixtures");

            migrationBuilder.DropColumn(
                name: "VenueName",
                table: "Fixtures");
        }
    }
}
