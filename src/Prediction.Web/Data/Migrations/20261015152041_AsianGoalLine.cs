using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AsianGoalLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tài xỉu bàn chuyển sang kèo châu Á (Bet365 "Goal Line", có mức 1/4, 3/4). Xóa bản ghi tài xỉu kiểu cũ (chỉ mức x.5)
            // của các trận chưa bóng lăn để lần lấy kèo sau ghi lại từ đầu, tránh báo "kèo dịch" / cờ đỏ giả do đổi nguồn.
            // Trận đã đá giữ nguyên lịch sử và kết quả chấm.
            migrationBuilder.Sql("""
                DELETE s FROM [OddsSnapshots] s
                INNER JOIN [Fixtures] f ON f.[Id] = s.[FixtureId]
                WHERE s.[Market] = 2 AND f.[KickoffUtc] > SYSUTCDATETIME() AND f.[Status] IN (N'NS', N'TBD');

                UPDATE c SET c.[TotalLine] = NULL, c.[Over] = NULL, c.[Under] = NULL
                FROM [OddsCheckpoints] c
                INNER JOIN [Fixtures] f ON f.[Id] = c.[FixtureId]
                WHERE f.[KickoffUtc] > SYSUTCDATETIME() AND f.[Status] IN (N'NS', N'TBD');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dữ liệu đã xóa không khôi phục được; kèo được lấy lại ở lần đồng bộ sau.
        }
    }
}
