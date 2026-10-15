using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class TeamDnaProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Analysis",
                table: "TeamDnas",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AvgCards",
                table: "TeamDnas",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AvgCornersAgainst",
                table: "TeamDnas",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AvgCornersFor",
                table: "TeamDnas",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AvgGoalsAgainst",
                table: "TeamDnas",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AvgGoalsFor",
                table: "TeamDnas",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AvgSecondHalfGoals",
                table: "TeamDnas",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AvgTotalGoals",
                table: "TeamDnas",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AwayLossPct",
                table: "TeamDnas",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BttsPct",
                table: "TeamDnas",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FirstHalfGoalPct",
                table: "TeamDnas",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HomeWinPct",
                table: "TeamDnas",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Matches",
                table: "TeamDnas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Over25Pct",
                table: "TeamDnas",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProfileUpdatedAtUtc",
                table: "TeamDnas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceLeagueId",
                table: "TeamDnas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceLeagueName",
                table: "TeamDnas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "TeamDnas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 34,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 35,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 36,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 40,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 42,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 45,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 47,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 48,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 49,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 50,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 51,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 55,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 65,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 66,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 157,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 160,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 162,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 163,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 164,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 165,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 167,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 168,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 169,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 170,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 172,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 182,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 194,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 197,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 201,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 209,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 211,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 212,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 228,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 247,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 257,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 415,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 487,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 494,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 495,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 497,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 499,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 500,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 502,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 503,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 505,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 511,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 529,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 530,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 531,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 532,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 533,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 536,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 538,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 541,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 546,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 547,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 611,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 645,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 727,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 798,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 1605,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 1608,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 1613,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 1616,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "TeamDnas",
                keyColumn: "TeamId",
                keyValue: 9568,
                columns: new[] { "Analysis", "AvgCards", "AvgCornersAgainst", "AvgCornersFor", "AvgGoalsAgainst", "AvgGoalsFor", "AvgSecondHalfGoals", "AvgTotalGoals", "AwayLossPct", "BttsPct", "FirstHalfGoalPct", "HomeWinPct", "Matches", "Over25Pct", "ProfileUpdatedAtUtc", "SourceLeagueId", "SourceLeagueName", "Summary" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Analysis",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "AvgCards",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "AvgCornersAgainst",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "AvgCornersFor",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "AvgGoalsAgainst",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "AvgGoalsFor",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "AvgSecondHalfGoals",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "AvgTotalGoals",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "AwayLossPct",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "BttsPct",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "FirstHalfGoalPct",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "HomeWinPct",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "Matches",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "Over25Pct",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "ProfileUpdatedAtUtc",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "SourceLeagueId",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "SourceLeagueName",
                table: "TeamDnas");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "TeamDnas");
        }
    }
}
