using Microsoft.EntityFrameworkCore;
using Prediction.Core.Entities;

namespace Prediction.Web.Data;

/// <summary>
/// DNA giải đấu và đội bóng theo file kỹ năng phân tích. Id đã đối chiếu với API-Football (10/2026).
/// Người dùng sửa được trên trang DNA.
/// </summary>
public static class DnaSeed
{
    private const DnaTag HomeStrongAwayWeak = DnaTag.HomeFortress | DnaTag.AwayFragile;

    public static void Apply(ModelBuilder b)
    {
        b.Entity<League>().HasData(
            new League { Id = 79, Name = "2. Bundesliga", Country = "Germany", Type = "League", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 88, Name = "Eredivisie", Country = "Netherlands", Type = "League", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 89, Name = "Eerste Divisie", Country = "Netherlands", Type = "League", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 253, Name = "Major League Soccer", Country = "USA", Type = "League", CurrentSeason = 2026, SeasonIsCalendarYear = true, IsTracked = true },
            new League { Id = 71, Name = "Serie A", Country = "Brazil", Type = "League", CurrentSeason = 2026, SeasonIsCalendarYear = true, IsTracked = true },
            new League { Id = 72, Name = "Serie B", Country = "Brazil", Type = "League", CurrentSeason = 2026, SeasonIsCalendarYear = true, IsTracked = true },
            new League { Id = 62, Name = "Ligue 2", Country = "France", Type = "League", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 141, Name = "Segunda División", Country = "Spain", Type = "League", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 17, Name = "AFC Champions League Elite", Country = "World", Type = "Cup", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 18, Name = "AFC Champions League Two", Country = "World", Type = "Cup", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 296, Name = "Thai League 1", Country = "Thailand", Type = "League", CurrentSeason = 2026, IsTracked = true },
            new League { Id = 297, Name = "Thai League 2", Country = "Thailand", Type = "League", CurrentSeason = 2026, IsTracked = true, HasStatsCoverage = false, HasInjuriesCoverage = false });

        b.Entity<LeagueDna>().HasData(
            L(78, "Bundesliga (Đức)", DnaTag.OverGoals, "Gegenpressing, hàng thủ dâng cao, ít câu giờ. ~3.10–3.35 bàn/trận. Ưu tiên Tài (H1 & FT) và BTTS; hạn chế Xỉu pre-match."),
            L(79, "2. Bundesliga (Đức)", DnaTag.OverGoals, "Phòng ngự chênh lệch, tâm lý tấn công phóng khoáng; bàn thắng dày cả H1 lẫn cuối H2. Ưu tiên Tài & BTTS."),
            L(88, "Eredivisie (Hà Lan)", DnaTag.OverGoals, "Tấn công tổng lực 4-3-3; cửa trên muốn thắng đậm. ~3.15–3.45 bàn/trận. Ưu tiên Tài (kể cả 3.0–3.5), Tài H1, cửa trên thắng cách biệt."),
            L(89, "Eerste Divisie (Hà Lan 2)", DnaTag.OverGoals, "Các đội trẻ (Jong Ajax, Jong PSV, Jong AZ, Jong Utrecht) đá cống hiến, phòng ngự lỏng. Ưu tiên Tài."),
            L(253, "MLS (Mỹ)", DnaTag.OverGoals | DnaTag.LateGoals, "Ngôi sao dồn cho hàng công, hàng thủ trung bình; thể lực sụp sau phút 65. ~2.95–3.15 bàn/trận. Ưu tiên Tài 2.75–3.0, BTTS, Tài H2 in-play."),
            L(207, "Super League (Thụy Sĩ)", DnaTag.OverGoals, "Phòng ngự chuyển trạng thái mỏng, lên bóng trực diện. Tài 2.75–3.25."),
            L(218, "Bundesliga (Áo)", DnaTag.OverGoals, "Phòng ngự chuyển trạng thái mỏng, lên bóng trực diện. Tài 2.75–3.25."),
            L(103, "Eliteserien (Na Uy)", DnaTag.OverGoals | DnaTag.OverCorners, "Tài bàn thắng 2.75–3.25 và Tài phạt góc."),
            L(188, "A-League (Úc)", DnaTag.OverGoals | DnaTag.OverCorners | DnaTag.LateGoals, "Tài bàn thắng, Tài góc; thể lực hàng thủ đuối cuối trận."),
            L(119, "Superliga (Đan Mạch)", DnaTag.OverGoals, "Tài bàn thắng 2.75–3.25."),
            L(72, "Serie B (Brazil)", DnaTag.HomeFortress | DnaTag.UnderGoals | DnaTag.LateGoals, "Khách bay chặng dài, tử thủ nhưng hụt hơi cuối trận. Chủ nhà chấp −0.25/−0.5 thắng kèo ~58–65%; ~2.00–2.15 bàn/trận. Ưu tiên cửa chủ nhà + Xỉu; kỵ đội khách chấp."),
            L(71, "Serie A (Brazil)", DnaTag.HomeFortress, "Lợi thế sân nhà rất lớn (di chuyển xa, khí hậu, mặt sân)."),
            L(128, "Liga Profesional (Argentina)", DnaTag.HomeFortress | DnaTag.UnderGoals | DnaTag.UnderCorners, "Khán đài cuồng nhiệt; kỷ luật phòng ngự, nhịp chậm. Chủ nhà thắng kèo, Xỉu bàn, Xỉu góc."),
            L(239, "Primera A (Colombia)", DnaTag.HomeFortress, "Độ cao và địa lý khắc nghiệt: cộng +0.50 đến +0.75 lợi thế sân nhà."),
            L(344, "Primera División (Bolivia)", DnaTag.HomeFortress, "Độ cao cực lớn: cộng +0.50 đến +0.75 lợi thế sân nhà."),
            L(242, "Liga Pro (Ecuador)", DnaTag.HomeFortress, "Độ cao: cộng +0.50 đến +0.75 lợi thế sân nhà."),
            L(281, "Primera División (Peru)", DnaTag.HomeFortress, "Lợi thế sân nhà lớn."),
            L(203, "Süper Lig (Thổ Nhĩ Kỳ)", DnaTag.HomeFortress, "Sức ép khán đài: ưu tiên chủ nhà thắng kèo châu Á và chấp góc chủ nhà."),
            L(62, "Ligue 2 (Pháp)", DnaTag.UnderGoals | DnaTag.UnderCorners, "Low-block, nhịp chậm, phạm lỗi chiến thuật. Xỉu 2.0–2.25, cửa dưới được chấp +0.25/+0.5, Xỉu góc, Tài thẻ."),
            L(141, "La Liga 2 (Tây Ban Nha)", DnaTag.UnderGoals | DnaTag.UnderCorners, "Thực dụng, kỷ luật phòng ngự. Xỉu bàn, cửa dưới được chấp, Xỉu góc, Tài thẻ."),
            L(197, "Super League (Hy Lạp)", DnaTag.UnderGoals, "Thực dụng: Xỉu bàn, cửa dưới được chấp, Tài thẻ."),
            L(290, "Persian Gulf Pro League (Iran)", DnaTag.UnderGoals, "Thực dụng: Xỉu bàn."),
            L(233, "Premier League (Ai Cập)", DnaTag.UnderGoals, "Thực dụng: Xỉu bàn."),
            L(39, "Premier League (Anh)", DnaTag.OverCorners, "Thiên hướng Tài góc, cày góc biên."),
            L(40, "Championship (Anh)", DnaTag.OverCorners, "Thiên hướng Tài góc."),
            L(179, "Premiership (Scotland)", DnaTag.OverCorners, "Thiên hướng Tài góc."),
            L(94, "Primeira Liga (Bồ Đào Nha)", DnaTag.OverCorners, "Tài góc khi nhóm Big 3 ép sân."),
            L(135, "Serie A (Ý)", DnaTag.UnderCorners, "Cặp giằng co trung lộ: thiên hướng Xỉu góc."));

        const string over = "Tấn công cường độ cao, dâng đội hình: chuyên Tài bàn thắng & BTTS.";
        const string fortress = "Mạnh sân nhà, hay thua kèo sân khách. Sân nhà: +0.25 tin cậy, sáng khi chấp nhẹ/được chấp. Sân khách làm cửa trên chấp −0.25 đến −0.75: CẢNH BÁO ĐỎ.";
        const string corners = "Cày góc mạnh: ưu tiên Tài góc & chấp góc.";
        const string pragmatic = "Thực dụng / phá lối chơi: ưu tiên Xỉu bàn hoặc chịu góc khi làm cửa dưới.";

        b.Entity<TeamDna>().HasData(
            T(499, "Atalanta", DnaTag.OverGoals | DnaTag.OverCorners, "Pressing 1-kèm-1 toàn sân, wing-back dâng cao, đôi công với mọi đối thủ: Tài 2.5–3.0 và Tài góc cao."),
            T(157, "Bayern München", DnaTag.OverGoals | DnaTag.OverCorners, over),
            T(168, "Bayer Leverkusen", DnaTag.OverGoals | DnaTag.OverCorners, over),
            T(165, "Borussia Dortmund", DnaTag.OverGoals, over),
            T(169, "Eintracht Frankfurt", DnaTag.OverGoals, over),
            T(172, "VfB Stuttgart", DnaTag.OverGoals, over),
            T(162, "Werder Bremen", DnaTag.OverGoals, over),
            T(167, "1899 Hoffenheim", DnaTag.OverGoals, over),
            T(197, "PSV Eindhoven", DnaTag.OverGoals, "Áp đặt một chiều, không dừng khi dẫn 1–2 bàn: Tài và kèo chấp sâu."),
            T(209, "Feyenoord", DnaTag.OverGoals, "Áp đặt một chiều: Tài và kèo chấp sâu."),
            T(194, "Ajax", DnaTag.OverGoals, "Áp đặt một chiều: Tài và kèo chấp sâu."),
            T(201, "AZ Alkmaar", DnaTag.OverGoals, "Áp đặt một chiều: Tài và kèo chấp sâu."),
            T(415, "Twente", DnaTag.OverGoals, "Áp đặt một chiều: Tài và kèo chấp sâu."),
            T(47, "Tottenham", DnaTag.OverGoals | DnaTag.OverCorners, "Lối chơi trực diện, bẫy việt vị cao: nhạy Tài bàn và Tài góc."),
            T(51, "Brighton", DnaTag.OverGoals | DnaTag.OverCorners, "Lối chơi trực diện: nhạy Tài bàn và Tài góc."),
            T(66, "Aston Villa", DnaTag.OverGoals, "Lối chơi trực diện: nhạy Tài bàn và Tài góc."),
            T(49, "Chelsea", DnaTag.OverGoals, "Lối chơi trực diện: nhạy Tài bàn và Tài góc."),
            T(35, "Bournemouth", DnaTag.OverGoals, "Lối chơi trực diện: nhạy Tài bàn và Tài góc."),
            T(529, "Barcelona", DnaTag.OverGoals, "Hỏa lực mạnh, dâng cao đội hình."),
            T(541, "Real Madrid", DnaTag.OverGoals | DnaTag.OverCorners, "Hỏa lực mạnh, tạt cánh tần suất cao."),
            T(533, "Villarreal", DnaTag.OverGoals, "Hỏa lực mạnh, dâng cao đội hình."),
            T(547, "Girona", DnaTag.OverGoals, "Hỏa lực mạnh, dâng cao đội hình."),
            T(9568, "Inter Miami", DnaTag.OverGoals, "Hàng công sắc, tuyến dưới hở 20 phút cuối: chuyên Tài."),
            T(1616, "Los Angeles FC", DnaTag.OverGoals, "Hàng công sắc, tuyến dưới hở 20 phút cuối: chuyên Tài."),
            T(1605, "Los Angeles Galaxy", DnaTag.OverGoals, "Hàng công sắc, tuyến dưới hở 20 phút cuối: chuyên Tài."),
            T(1613, "Columbus Crew", DnaTag.OverGoals, "Hàng công sắc, tuyến dưới hở 20 phút cuối: chuyên Tài."),
            T(1608, "Atlanta United FC", DnaTag.OverGoals, "Hàng công sắc, tuyến dưới hở 20 phút cuối: chuyên Tài."),
            T(34, "Newcastle", HomeStrongAwayWeak | DnaTag.OverCorners, "St James' Park: pressing nghẹt thở, thắng kèo châu Á và kèo góc sân nhà rất cao. Sân khách: hay thua kèo, nguy hiểm khi chấp −0.25 đến −0.75."),
            T(36, "Fulham", HomeStrongAwayWeak, fortress),
            T(55, "Brentford", HomeStrongAwayWeak, fortress),
            T(45, "Everton", HomeStrongAwayWeak, fortress),
            T(65, "Nottingham Forest", HomeStrongAwayWeak, fortress),
            T(48, "West Ham", HomeStrongAwayWeak, fortress),
            T(531, "Athletic Club", HomeStrongAwayWeak | DnaTag.OverCorners, "Cực mạnh tại San Mamés, giảm sút khi đi khách; tạt cánh nhiều."),
            T(727, "Osasuna", HomeStrongAwayWeak, fortress),
            T(532, "Valencia", HomeStrongAwayWeak, fortress),
            T(536, "Sevilla", HomeStrongAwayWeak, fortress),
            T(546, "Getafe", HomeStrongAwayWeak | DnaTag.Pragmatic, fortress + " " + pragmatic),
            T(538, "Celta Vigo", HomeStrongAwayWeak, fortress),
            T(487, "Lazio", HomeStrongAwayWeak, fortress),
            T(502, "Fiorentina", HomeStrongAwayWeak, fortress),
            T(500, "Bologna", HomeStrongAwayWeak, fortress),
            T(497, "AS Roma", HomeStrongAwayWeak, fortress),
            T(494, "Udinese", HomeStrongAwayWeak, fortress),
            T(503, "Torino", HomeStrongAwayWeak | DnaTag.Pragmatic, fortress + " " + pragmatic),
            T(163, "Borussia Mönchengladbach", HomeStrongAwayWeak, fortress),
            T(182, "Union Berlin", HomeStrongAwayWeak, fortress),
            T(164, "FSV Mainz 05", HomeStrongAwayWeak, fortress),
            T(160, "SC Freiburg", HomeStrongAwayWeak, fortress),
            T(170, "FC Augsburg", HomeStrongAwayWeak, fortress),
            T(645, "Galatasaray", HomeStrongAwayWeak, "Chảo lửa sân nhà; làm khách ở cúp châu Âu thường thua kèo sâu."),
            T(611, "Fenerbahçe", HomeStrongAwayWeak, "Chảo lửa sân nhà; làm khách ở cúp châu Âu thường thua kèo sâu."),
            T(247, "Celtic", HomeStrongAwayWeak, "Chảo lửa sân nhà; làm khách ở cúp châu Âu thường thua kèo sâu."),
            T(257, "Rangers", HomeStrongAwayWeak, "Chảo lửa sân nhà; làm khách ở cúp châu Âu thường thua kèo sâu."),
            T(50, "Manchester City", DnaTag.OverCorners, corners),
            T(40, "Liverpool", DnaTag.OverCorners, corners),
            T(42, "Arsenal", DnaTag.OverCorners, corners),
            T(505, "Inter", DnaTag.OverCorners, corners),
            T(228, "Sporting CP", DnaTag.OverCorners, corners),
            T(211, "Benfica", DnaTag.OverCorners, corners),
            T(212, "FC Porto", DnaTag.OverCorners, corners),
            T(530, "Atletico Madrid", DnaTag.Pragmatic, "Thực dụng khi làm khách trận cầu đinh: Xỉu bàn."),
            T(798, "Mallorca", DnaTag.Pragmatic, pragmatic),
            T(495, "Genoa", DnaTag.Pragmatic, pragmatic),
            T(511, "Empoli", DnaTag.Pragmatic, pragmatic));
    }

    private static LeagueDna L(int id, string name, DnaTag tags, string notes) => new() { LeagueId = id, LeagueName = name, Tags = tags, Notes = notes };

    private static TeamDna T(int id, string name, DnaTag tags, string notes) => new() { TeamId = id, TeamName = name, Tags = tags, Notes = notes };
}
