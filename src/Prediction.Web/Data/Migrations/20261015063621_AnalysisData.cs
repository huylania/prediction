using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Prediction.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AnalysisData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CoachCheckedAtUtc",
                table: "Teams",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasInjuriesCoverage",
                table: "Leagues",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasStatsCoverage",
                table: "Leagues",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SeasonIsCalendarYear",
                table: "Leagues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FixtureInjuries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixtureInjuries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixtureInjuries_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FixtureStats",
                columns: table => new
                {
                    FixtureId = table.Column<int>(type: "int", nullable: false),
                    HomeCorners = table.Column<int>(type: "int", nullable: true),
                    AwayCorners = table.Column<int>(type: "int", nullable: true),
                    HomeShots = table.Column<int>(type: "int", nullable: true),
                    AwayShots = table.Column<int>(type: "int", nullable: true),
                    HomeShotsOnTarget = table.Column<int>(type: "int", nullable: true),
                    AwayShotsOnTarget = table.Column<int>(type: "int", nullable: true),
                    HomeXg = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    AwayXg = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    HomePossession = table.Column<int>(type: "int", nullable: true),
                    AwayPossession = table.Column<int>(type: "int", nullable: true),
                    HomeYellow = table.Column<int>(type: "int", nullable: true),
                    AwayYellow = table.Column<int>(type: "int", nullable: true),
                    HomeRed = table.Column<int>(type: "int", nullable: true),
                    AwayRed = table.Column<int>(type: "int", nullable: true),
                    HalftimeHome = table.Column<int>(type: "int", nullable: true),
                    HalftimeAway = table.Column<int>(type: "int", nullable: true),
                    GoalTimeline = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixtureStats", x => x.FixtureId);
                    table.ForeignKey(
                        name: "FK_FixtureStats_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueDnas",
                columns: table => new
                {
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    LeagueName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tags = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueDnas", x => x.LeagueId);
                });

            migrationBuilder.CreateTable(
                name: "TeamChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PersonName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsIgnored = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamChanges_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamDnas",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tags = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamDnas", x => x.TeamId);
                });

            migrationBuilder.InsertData(
                table: "LeagueDnas",
                columns: new[] { "LeagueId", "LeagueName", "Notes", "Tags" },
                values: new object[,]
                {
                    { 39, "Premier League (Anh)", "Thiên hướng Tài góc, cày góc biên.", 16 },
                    { 40, "Championship (Anh)", "Thiên hướng Tài góc.", 16 },
                    { 62, "Ligue 2 (Pháp)", "Low-block, nhịp chậm, phạm lỗi chiến thuật. Xỉu 2.0–2.25, cửa dưới được chấp +0.25/+0.5, Xỉu góc, Tài thẻ.", 34 },
                    { 71, "Serie A (Brazil)", "Lợi thế sân nhà rất lớn (di chuyển xa, khí hậu, mặt sân).", 4 },
                    { 72, "Serie B (Brazil)", "Khách bay chặng dài, tử thủ nhưng hụt hơi cuối trận. Chủ nhà chấp −0.25/−0.5 thắng kèo ~58–65%; ~2.00–2.15 bàn/trận. Ưu tiên cửa chủ nhà + Xỉu; kỵ đội khách chấp.", 70 },
                    { 78, "Bundesliga (Đức)", "Gegenpressing, hàng thủ dâng cao, ít câu giờ. ~3.10–3.35 bàn/trận. Ưu tiên Tài (H1 & FT) và BTTS; hạn chế Xỉu pre-match.", 1 },
                    { 79, "2. Bundesliga (Đức)", "Phòng ngự chênh lệch, tâm lý tấn công phóng khoáng; bàn thắng dày cả H1 lẫn cuối H2. Ưu tiên Tài & BTTS.", 1 },
                    { 88, "Eredivisie (Hà Lan)", "Tấn công tổng lực 4-3-3; cửa trên muốn thắng đậm. ~3.15–3.45 bàn/trận. Ưu tiên Tài (kể cả 3.0–3.5), Tài H1, cửa trên thắng cách biệt.", 1 },
                    { 89, "Eerste Divisie (Hà Lan 2)", "Các đội trẻ (Jong Ajax, Jong PSV, Jong AZ, Jong Utrecht) đá cống hiến, phòng ngự lỏng. Ưu tiên Tài.", 1 },
                    { 94, "Primeira Liga (Bồ Đào Nha)", "Tài góc khi nhóm Big 3 ép sân.", 16 },
                    { 103, "Eliteserien (Na Uy)", "Tài bàn thắng 2.75–3.25 và Tài phạt góc.", 17 },
                    { 119, "Superliga (Đan Mạch)", "Tài bàn thắng 2.75–3.25.", 1 },
                    { 128, "Liga Profesional (Argentina)", "Khán đài cuồng nhiệt; kỷ luật phòng ngự, nhịp chậm. Chủ nhà thắng kèo, Xỉu bàn, Xỉu góc.", 38 },
                    { 135, "Serie A (Ý)", "Cặp giằng co trung lộ: thiên hướng Xỉu góc.", 32 },
                    { 141, "La Liga 2 (Tây Ban Nha)", "Thực dụng, kỷ luật phòng ngự. Xỉu bàn, cửa dưới được chấp, Xỉu góc, Tài thẻ.", 34 },
                    { 179, "Premiership (Scotland)", "Thiên hướng Tài góc.", 16 },
                    { 188, "A-League (Úc)", "Tài bàn thắng, Tài góc; thể lực hàng thủ đuối cuối trận.", 81 },
                    { 197, "Super League (Hy Lạp)", "Thực dụng: Xỉu bàn, cửa dưới được chấp, Tài thẻ.", 2 },
                    { 203, "Süper Lig (Thổ Nhĩ Kỳ)", "Sức ép khán đài: ưu tiên chủ nhà thắng kèo châu Á và chấp góc chủ nhà.", 4 },
                    { 207, "Super League (Thụy Sĩ)", "Phòng ngự chuyển trạng thái mỏng, lên bóng trực diện. Tài 2.75–3.25.", 1 },
                    { 218, "Bundesliga (Áo)", "Phòng ngự chuyển trạng thái mỏng, lên bóng trực diện. Tài 2.75–3.25.", 1 },
                    { 233, "Premier League (Ai Cập)", "Thực dụng: Xỉu bàn.", 2 },
                    { 239, "Primera A (Colombia)", "Độ cao và địa lý khắc nghiệt: cộng +0.50 đến +0.75 lợi thế sân nhà.", 4 },
                    { 242, "Liga Pro (Ecuador)", "Độ cao: cộng +0.50 đến +0.75 lợi thế sân nhà.", 4 },
                    { 253, "MLS (Mỹ)", "Ngôi sao dồn cho hàng công, hàng thủ trung bình; thể lực sụp sau phút 65. ~2.95–3.15 bàn/trận. Ưu tiên Tài 2.75–3.0, BTTS, Tài H2 in-play.", 65 },
                    { 281, "Primera División (Peru)", "Lợi thế sân nhà lớn.", 4 },
                    { 290, "Persian Gulf Pro League (Iran)", "Thực dụng: Xỉu bàn.", 2 },
                    { 344, "Primera División (Bolivia)", "Độ cao cực lớn: cộng +0.50 đến +0.75 lợi thế sân nhà.", 4 }
                });

            migrationBuilder.UpdateData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "HasInjuriesCoverage", "HasStatsCoverage", "SeasonIsCalendarYear" },
                values: new object[] { true, true, false });

            migrationBuilder.UpdateData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 39,
                columns: new[] { "HasInjuriesCoverage", "HasStatsCoverage", "SeasonIsCalendarYear" },
                values: new object[] { true, true, false });

            migrationBuilder.UpdateData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 61,
                columns: new[] { "HasInjuriesCoverage", "HasStatsCoverage", "SeasonIsCalendarYear" },
                values: new object[] { true, true, false });

            migrationBuilder.UpdateData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 78,
                columns: new[] { "HasInjuriesCoverage", "HasStatsCoverage", "SeasonIsCalendarYear" },
                values: new object[] { true, true, false });

            migrationBuilder.UpdateData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 135,
                columns: new[] { "HasInjuriesCoverage", "HasStatsCoverage", "SeasonIsCalendarYear" },
                values: new object[] { true, true, false });

            migrationBuilder.UpdateData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 140,
                columns: new[] { "HasInjuriesCoverage", "HasStatsCoverage", "SeasonIsCalendarYear" },
                values: new object[] { true, true, false });

            migrationBuilder.UpdateData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 340,
                columns: new[] { "HasInjuriesCoverage", "HasStatsCoverage", "SeasonIsCalendarYear" },
                values: new object[] { true, true, false });

            // Giải có thể đã được thêm tay trong Cài đặt: chỉ chèn giải chưa có để migration không lỗi trùng khóa.
            foreach (var (id, country, name, calendarYear) in new[]
            {
                (62, "France", "Ligue 2", false),
                (71, "Brazil", "Serie A", true),
                (72, "Brazil", "Serie B", true),
                (79, "Germany", "2. Bundesliga", false),
                (88, "Netherlands", "Eredivisie", false),
                (89, "Netherlands", "Eerste Divisie", false),
                (141, "Spain", "Segunda División", false),
                (253, "USA", "Major League Soccer", true),
            })
            {
                migrationBuilder.Sql(
                    $"IF NOT EXISTS (SELECT 1 FROM [Leagues] WHERE [Id] = {id}) " +
                    "INSERT INTO [Leagues] ([Id], [Country], [CurrentSeason], [HasInjuriesCoverage], [HasStatsCoverage], [IsDemo], [IsTracked], [LogoUrl], [Name], [SeasonIsCalendarYear], [SeasonYearIsEndYear], [Type]) " +
                    $"VALUES ({id}, N'{country}', 2026, 1, 1, 0, 1, NULL, N'{name}', {(calendarYear ? 1 : 0)}, 0, N'League');");
            }

            migrationBuilder.Sql("UPDATE [Leagues] SET [SeasonIsCalendarYear] = 1 WHERE [Id] IN (71, 72, 253);");

            migrationBuilder.InsertData(
                table: "TeamDnas",
                columns: new[] { "TeamId", "Notes", "Tags", "TeamName" },
                values: new object[,]
                {
                    { 34, "St James' Park: pressing nghẹt thở, thắng kèo châu Á và kèo góc sân nhà rất cao. Sân khách: hay thua kèo, nguy hiểm khi chấp −0.25 đến −0.75.", 28, "Newcastle" },
                    { 35, "Lối chơi trực diện: nhạy Tài bàn và Tài góc.", 1, "Bournemouth" },
                    { 36, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Fulham" },
                    { 40, "Cày góc mạnh: ưu tiên Tài góc & chấp góc.", 16, "Liverpool" },
                    { 42, "Cày góc mạnh: ưu tiên Tài góc & chấp góc.", 16, "Arsenal" },
                    { 45, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Everton" },
                    { 47, "Lối chơi trực diện, bẫy việt vị cao: nhạy Tài bàn và Tài góc.", 17, "Tottenham" },
                    { 48, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "West Ham" },
                    { 49, "Lối chơi trực diện: nhạy Tài bàn và Tài góc.", 1, "Chelsea" },
                    { 50, "Cày góc mạnh: ưu tiên Tài góc & chấp góc.", 16, "Manchester City" },
                    { 51, "Lối chơi trực diện: nhạy Tài bàn và Tài góc.", 17, "Brighton" },
                    { 55, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Brentford" },
                    { 65, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Nottingham Forest" },
                    { 66, "Lối chơi trực diện: nhạy Tài bàn và Tài góc.", 1, "Aston Villa" },
                    { 157, "Tấn công cường độ cao, dâng đội hình: chuyên Tài bàn thắng & BTTS.", 17, "Bayern München" },
                    { 160, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "SC Freiburg" },
                    { 162, "Tấn công cường độ cao, dâng đội hình: chuyên Tài bàn thắng & BTTS.", 1, "Werder Bremen" },
                    { 163, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Borussia Mönchengladbach" },
                    { 164, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "FSV Mainz 05" },
                    { 165, "Tấn công cường độ cao, dâng đội hình: chuyên Tài bàn thắng & BTTS.", 1, "Borussia Dortmund" },
                    { 167, "Tấn công cường độ cao, dâng đội hình: chuyên Tài bàn thắng & BTTS.", 1, "1899 Hoffenheim" },
                    { 168, "Tấn công cường độ cao, dâng đội hình: chuyên Tài bàn thắng & BTTS.", 17, "Bayer Leverkusen" },
                    { 169, "Tấn công cường độ cao, dâng đội hình: chuyên Tài bàn thắng & BTTS.", 1, "Eintracht Frankfurt" },
                    { 170, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "FC Augsburg" },
                    { 172, "Tấn công cường độ cao, dâng đội hình: chuyên Tài bàn thắng & BTTS.", 1, "VfB Stuttgart" },
                    { 182, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Union Berlin" },
                    { 194, "Áp đặt một chiều: Tài và kèo chấp sâu.", 1, "Ajax" },
                    { 197, "Áp đặt một chiều, không dừng khi dẫn 1–2 bàn: Tài và kèo chấp sâu.", 1, "PSV Eindhoven" },
                    { 201, "Áp đặt một chiều: Tài và kèo chấp sâu.", 1, "AZ Alkmaar" },
                    { 209, "Áp đặt một chiều: Tài và kèo chấp sâu.", 1, "Feyenoord" },
                    { 211, "Cày góc mạnh: ưu tiên Tài góc & chấp góc.", 16, "Benfica" },
                    { 212, "Cày góc mạnh: ưu tiên Tài góc & chấp góc.", 16, "FC Porto" },
                    { 228, "Cày góc mạnh: ưu tiên Tài góc & chấp góc.", 16, "Sporting CP" },
                    { 247, "Chảo lửa sân nhà; làm khách ở cúp châu Âu thường thua kèo sâu.", 12, "Celtic" },
                    { 257, "Chảo lửa sân nhà; làm khách ở cúp châu Âu thường thua kèo sâu.", 12, "Rangers" },
                    { 415, "Áp đặt một chiều: Tài và kèo chấp sâu.", 1, "Twente" },
                    { 487, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Lazio" },
                    { 494, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Udinese" },
                    { 495, "Thực dụng / phá lối chơi: ưu tiên Xỉu bàn hoặc chịu góc khi làm cửa dưới.", 128, "Genoa" },
                    { 497, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "AS Roma" },
                    { 499, "Pressing 1-kèm-1 toàn sân, wing-back dâng cao, đôi công với mọi đối thủ: Tài 2.5–3.0 và Tài góc cao.", 17, "Atalanta" },
                    { 500, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Bologna" },
                    { 502, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Fiorentina" },
                    { 503, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ. Thực dụng / phá lối chơi: ưu tiên Xỉu bàn hoặc chịu góc khi làm cửa dưới.", 140, "Torino" },
                    { 505, "Cày góc mạnh: ưu tiên Tài góc & chấp góc.", 16, "Inter" },
                    { 511, "Thực dụng / phá lối chơi: ưu tiên Xỉu bàn hoặc chịu góc khi làm cửa dưới.", 128, "Empoli" },
                    { 529, "Hỏa lực mạnh, dâng cao đội hình.", 1, "Barcelona" },
                    { 530, "Thực dụng khi làm khách trận cầu đinh: Xỉu bàn.", 128, "Atletico Madrid" },
                    { 531, "Cực mạnh tại San Mamés, giảm sút khi đi khách; tạt cánh nhiều.", 28, "Athletic Club" },
                    { 532, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Valencia" },
                    { 533, "Hỏa lực mạnh, dâng cao đội hình.", 1, "Villarreal" },
                    { 536, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Sevilla" },
                    { 538, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Celta Vigo" },
                    { 541, "Hỏa lực mạnh, tạt cánh tần suất cao.", 17, "Real Madrid" },
                    { 546, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ. Thực dụng / phá lối chơi: ưu tiên Xỉu bàn hoặc chịu góc khi làm cửa dưới.", 140, "Getafe" },
                    { 547, "Hỏa lực mạnh, dâng cao đội hình.", 1, "Girona" },
                    { 611, "Chảo lửa sân nhà; làm khách ở cúp châu Âu thường thua kèo sâu.", 12, "Fenerbahçe" },
                    { 645, "Chảo lửa sân nhà; làm khách ở cúp châu Âu thường thua kèo sâu.", 12, "Galatasaray" },
                    { 727, "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.", 12, "Osasuna" },
                    { 798, "Thực dụng / phá lối chơi: ưu tiên Xỉu bàn hoặc chịu góc khi làm cửa dưới.", 128, "Mallorca" },
                    { 1605, "Hàng công sắc, tuyến dưới hở 20 phút cuối: chuyên Tài.", 1, "Los Angeles Galaxy" },
                    { 1608, "Hàng công sắc, tuyến dưới hở 20 phút cuối: chuyên Tài.", 1, "Atlanta United FC" },
                    { 1613, "Hàng công sắc, tuyến dưới hở 20 phút cuối: chuyên Tài.", 1, "Columbus Crew" },
                    { 1616, "Hàng công sắc, tuyến dưới hở 20 phút cuối: chuyên Tài.", 1, "Los Angeles FC" },
                    { 9568, "Hàng công sắc, tuyến dưới hở 20 phút cuối: chuyên Tài.", 1, "Inter Miami" }
                });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: 33,
                column: "CoachCheckedAtUtc",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_FixtureInjuries_FixtureId",
                table: "FixtureInjuries",
                column: "FixtureId");

            migrationBuilder.CreateIndex(
                name: "IX_FixtureStats_IsFinal",
                table: "FixtureStats",
                column: "IsFinal");

            migrationBuilder.CreateIndex(
                name: "IX_TeamChanges_TeamId_Kind",
                table: "TeamChanges",
                columns: new[] { "TeamId", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FixtureInjuries");

            migrationBuilder.DropTable(
                name: "FixtureStats");

            migrationBuilder.DropTable(
                name: "LeagueDnas");

            migrationBuilder.DropTable(
                name: "TeamChanges");

            migrationBuilder.DropTable(
                name: "TeamDnas");

            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 141);

            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Id",
                keyValue: 253);

            migrationBuilder.DropColumn(
                name: "CoachCheckedAtUtc",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "HasInjuriesCoverage",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "HasStatsCoverage",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "SeasonIsCalendarYear",
                table: "Leagues");
        }
    }
}
