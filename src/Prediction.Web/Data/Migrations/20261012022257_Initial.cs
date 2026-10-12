using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Leagues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CurrentSeason = table.Column<int>(type: "int", nullable: false),
                    IsTracked = table.Column<bool>(type: "bit", nullable: false),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leagues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Job = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Ok = table.Column<bool>(type: "bit", nullable: false),
                    Requests = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fixtures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    Season = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    KickoffUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    HomeTeamId = table.Column<int>(type: "int", nullable: false),
                    AwayTeamId = table.Column<int>(type: "int", nullable: false),
                    HomeGoals = table.Column<int>(type: "int", nullable: true),
                    AwayGoals = table.Column<int>(type: "int", nullable: true),
                    HasTable = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fixtures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fixtures_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fixtures_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fixtures_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeagueId = table.Column<int>(type: "int", nullable: true),
                    Market = table.Column<int>(type: "int", nullable: false),
                    Selection = table.Column<int>(type: "int", nullable: false),
                    SubjectTeamId = table.Column<int>(type: "int", nullable: true),
                    SubjectVenue = table.Column<int>(type: "int", nullable: false),
                    MinRankGap = table.Column<int>(type: "int", nullable: true),
                    MaxRankGap = table.Column<int>(type: "int", nullable: true),
                    SubjectMaxRank = table.Column<int>(type: "int", nullable: true),
                    OpponentMinRank = table.Column<int>(type: "int", nullable: true),
                    MinPointsGap = table.Column<int>(type: "int", nullable: true),
                    SubjectMinFormPoints = table.Column<int>(type: "int", nullable: true),
                    OpponentMaxFormPoints = table.Column<int>(type: "int", nullable: true),
                    MinAvgTotalGoals = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: true),
                    MaxAvgTotalGoals = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: true),
                    MinMatchesPlayed = table.Column<int>(type: "int", nullable: true),
                    AhLineMin = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    AhLineMax = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    OuLineMin = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    OuLineMax = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    MinOdds = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: true),
                    AhMovement = table.Column<int>(type: "int", nullable: false),
                    MinAhLineMove = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    MinPriceDrop = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: true),
                    OuMovement = table.Column<int>(type: "int", nullable: false),
                    MinOuLineMove = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    RequireApiWinnerSubject = table.Column<bool>(type: "bit", nullable: false),
                    MinApiWinPercentSubject = table.Column<int>(type: "int", nullable: true),
                    RequireApiUnderOverAgree = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rules_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Rules_Teams_SubjectTeamId",
                        column: x => x.SubjectTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ApiAdvices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WinnerTeamId = table.Column<int>(type: "int", nullable: true),
                    WinnerComment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WinOrDraw = table.Column<bool>(type: "bit", nullable: true),
                    UnderOver = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Advice = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PercentHome = table.Column<int>(type: "int", nullable: true),
                    PercentDraw = table.Column<int>(type: "int", nullable: true),
                    PercentAway = table.Column<int>(type: "int", nullable: true),
                    FormHome = table.Column<int>(type: "int", nullable: true),
                    FormAway = table.Column<int>(type: "int", nullable: true),
                    AttackHome = table.Column<int>(type: "int", nullable: true),
                    AttackAway = table.Column<int>(type: "int", nullable: true),
                    DefenceHome = table.Column<int>(type: "int", nullable: true),
                    DefenceAway = table.Column<int>(type: "int", nullable: true),
                    H2HHome = table.Column<int>(type: "int", nullable: true),
                    H2HAway = table.Column<int>(type: "int", nullable: true),
                    TotalHome = table.Column<int>(type: "int", nullable: true),
                    TotalAway = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiAdvices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiAdvices_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OddsSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    Market = table.Column<int>(type: "int", nullable: false),
                    Line = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    PriceA = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    PriceB = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    BookmakerId = table.Column<int>(type: "int", nullable: false),
                    BookmakerUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OddsSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OddsSnapshots_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Suggestions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleId = table.Column<int>(type: "int", nullable: false),
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    FirstLine = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    FirstOdds = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    FirstMatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Line = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Odds = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    SubjectRank = table.Column<int>(type: "int", nullable: true),
                    OpponentRank = table.Column<int>(type: "int", nullable: true),
                    CurrentlyMatches = table.Column<bool>(type: "bit", nullable: false),
                    IsFrozen = table.Column<bool>(type: "bit", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Profit = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Suggestions_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Suggestions_Rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "Rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Leagues",
                columns: new[] { "Id", "Country", "CurrentSeason", "IsDemo", "IsTracked", "LogoUrl", "Name", "Type" },
                values: new object[,]
                {
                    { 2, "World", 2026, false, true, null, "UEFA Champions League", "Cup" },
                    { 39, "England", 2026, false, true, null, "Premier League", "League" },
                    { 61, "France", 2026, false, true, null, "Ligue 1", "League" },
                    { 78, "Germany", 2026, false, true, null, "Bundesliga", "League" },
                    { 135, "Italy", 2026, false, true, null, "Serie A", "League" },
                    { 140, "Spain", 2026, false, true, null, "La Liga", "League" },
                    { 340, "Vietnam", 2026, false, true, null, "V.League 1", "League" }
                });

            migrationBuilder.InsertData(
                table: "Teams",
                columns: new[] { "Id", "LogoUrl", "Name" },
                values: new object[] { 33, null, "Manchester United" });

            migrationBuilder.InsertData(
                table: "Rules",
                columns: new[] { "Id", "AhLineMax", "AhLineMin", "AhMovement", "CreatedAtUtc", "IsActive", "LeagueId", "Market", "MaxAvgTotalGoals", "MaxRankGap", "MinAhLineMove", "MinApiWinPercentSubject", "MinAvgTotalGoals", "MinMatchesPlayed", "MinOdds", "MinOuLineMove", "MinPointsGap", "MinPriceDrop", "MinRankGap", "Name", "Notes", "OpponentMaxFormPoints", "OpponentMinRank", "OuLineMax", "OuLineMin", "OuMovement", "RequireApiUnderOverAgree", "RequireApiWinnerSubject", "Selection", "SubjectMaxRank", "SubjectMinFormPoints", "SubjectTeamId", "SubjectVenue" },
                values: new object[] { 1, null, null, 0, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 39, 1, null, null, null, null, null, 5, null, null, null, null, 1, "MU gặp đội xếp dưới → chọn MU", "Ví dụ từ kinh nghiệm: MU gặp đội thấp hơn trên BXH thì chọn MU (kèo chấp). Sửa điều kiện mức chấp, phong độ... cho đúng thói quen của bạn rồi chạy Kiểm chứng.", null, null, null, null, 0, false, false, 1, null, null, 33, 0 });

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_Username",
                table: "AdminUsers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiAdvices_FixtureId",
                table: "ApiAdvices",
                column: "FixtureId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_AwayTeamId",
                table: "Fixtures",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_HomeTeamId",
                table: "Fixtures",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_KickoffUtc",
                table: "Fixtures",
                column: "KickoffUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_LeagueId_Season",
                table: "Fixtures",
                columns: new[] { "LeagueId", "Season" });

            migrationBuilder.CreateIndex(
                name: "IX_OddsSnapshots_FixtureId_Market_FetchedAtUtc",
                table: "OddsSnapshots",
                columns: new[] { "FixtureId", "Market", "FetchedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Rules_LeagueId",
                table: "Rules",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_SubjectTeamId",
                table: "Rules",
                column: "SubjectTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Suggestions_FixtureId",
                table: "Suggestions",
                column: "FixtureId");

            migrationBuilder.CreateIndex(
                name: "IX_Suggestions_RuleId_FixtureId_Side",
                table: "Suggestions",
                columns: new[] { "RuleId", "FixtureId", "Side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_StartedAtUtc",
                table: "SyncLogs",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Name",
                table: "Teams",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminUsers");

            migrationBuilder.DropTable(
                name: "ApiAdvices");

            migrationBuilder.DropTable(
                name: "OddsSnapshots");

            migrationBuilder.DropTable(
                name: "Suggestions");

            migrationBuilder.DropTable(
                name: "SyncLogs");

            migrationBuilder.DropTable(
                name: "Fixtures");

            migrationBuilder.DropTable(
                name: "Rules");

            migrationBuilder.DropTable(
                name: "Leagues");

            migrationBuilder.DropTable(
                name: "Teams");
        }
    }
}
