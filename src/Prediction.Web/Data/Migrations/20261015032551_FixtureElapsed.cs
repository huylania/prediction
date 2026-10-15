using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixtureElapsed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Elapsed",
                table: "Fixtures",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Elapsed",
                table: "Fixtures");
        }
    }
}
