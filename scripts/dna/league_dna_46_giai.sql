-- DNA giải đấu 46 giải (số liệu 1 năm) → dbo.LeagueDnas
-- Sinh tự động bởi scripts/dna/gen_league_dna_sql.py ngày 2026-10-15 từ dna_giai_dau_46_giai_1nam.csv.
-- CHẠY SAU KHI website bản mới đã khởi động ít nhất 1 lần (migration LeagueDnaProfile thêm cột hồ sơ, bật theo dõi các giải mới).
-- Giải đã có DNA: giữ các tích cũ + cộng thêm tích từ file (phép OR), giữ Tên và Ghi chú; ghi đè hồ sơ 1 năm.
-- Giải chưa có: thêm mới. Chạy lại nhiều lần vẫn cho cùng kết quả.
-- Cách chạy: mở bằng SSMS / Azure Data Studio rồi Execute, hoặc: sqlcmd -S <máy> -d <CSDL> -U <user> -P <mật khẩu> -f 65001 -i league_dna_46_giai.sql
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Kiểm tra cột mới; thiếu thì dừng (MERGE chạy bằng sp_executesql nên không báo lỗi tên cột lẫn vào).
IF COL_LENGTH('dbo.LeagueDnas', 'Analysis') IS NULL
BEGIN
    RAISERROR(N'Bảng LeagueDnas chưa có cột hồ sơ 1 năm. Hãy cập nhật và chạy website bản mới trước (migration LeagueDnaProfile), rồi chạy lại script này.', 16, 1);
    RETURN;
END;

IF OBJECT_ID('tempdb..#src') IS NOT NULL DROP TABLE #src;
CREATE TABLE #src (
    LeagueId int NOT NULL PRIMARY KEY, LeagueName nvarchar(200) NOT NULL, Country nvarchar(100) NULL, Tags int NOT NULL,
    AvgGoals decimal(5,2) NULL, Over25Pct decimal(5,1) NULL, DrawPct decimal(5,1) NULL, HomeWinPct decimal(5,1) NULL,
    AvgCorners decimal(5,2) NULL, FirstHalfGoalPct decimal(5,1) NULL, LateGoalPct decimal(5,1) NULL, AvgCards decimal(5,2) NULL,
    Summary nvarchar(500) NULL, Analysis nvarchar(2000) NULL);

INSERT INTO #src (LeagueId, LeagueName, Country, Tags, AvgGoals, Over25Pct, DrawPct, HomeWinPct, AvgCorners, FirstHalfGoalPct, LateGoalPct, AvgCards, Summary, Analysis) VALUES
(204, N'1. Lig (Turkey)', N'Turkey', 12356, 2.76, 54.5, 25.5, 46.5, 9.80, 71.0, 42.0, 5.20, N'Chảo lửa sân nhà, đá rát nhiều thẻ đỏ & hay có bàn phút bù giờ.', N'Chủ nhà thắng 46.5% nhờ áp lực khán đài lớn; TB 5.2 thẻ/trận (rất dễ có thẻ đỏ và Penalty làm đảo chiều kèo); 42% số trận có bàn sau phút 75 do mất tập trung cuối trận.'),
(79, N'2. Bundesliga (Germany)', N'Germany', 10321, 3.18, 63.2, 24.0, 44.0, 10.40, 78.5, 44.5, 4.30, N'Phòng ngự chênh lệch, cởi mở 2 đầu: chuyên Tài bàn, Tài H1 & Tài góc; kèo trên chấp sâu dễ sảy chân.', N'TB 3.18 bàn/trận (63.2% nổ Tài 2.5), 78.5% trận có bàn ngay Hiệp 1 và TB 10.4 góc/trận. Trình độ các đội rất sát nhau nên đội đầu bảng chấp sâu hay bị cửa dưới cầm hòa/lật kèo.'),
(17, N'AFC Champions League Elite (World)', N'World', 2389, 3.15, 62.5, 21.0, 52.0, 10.20, 75.0, 43.0, 4.10, N'Chênh lệch trình độ & ngoại binh lớn: ưu tiên Tài bàn, Chủ nhà & Kèo trên chấp sâu.', N'TB 3.15 bàn/trận (62.5% nổ Tài 2.5). Các đại gia Tây Á (Ả Rập) và Đông Á (Nhật/Hàn) vượt trội kèm lợi thế di chuyển sân nhà (52% chủ nhà thắng), cuối trận đội yếu dễ vỡ trận (43% nổ bàn sau phút 75).'),
(18, N'AFC Champions League Two (World)', N'World', 2373, 3.10, 60.8, 22.0, 51.5, 9.90, 74.0, 41.5, 4.00, N'Phân hóa đẳng cấp rõ rệt ở vòng bảng: chuyên Tài bàn thắng, mạnh sân nhà & tín kèo trên.', N'TB 3.10 bàn/trận (60.8% nổ Tài 2.5); chênh lệch lớn giữa các CLB có dàn ngoại binh đắt giá và các đội Đông Nam Á/Trung Á giúp kèo trên và chủ nhà (thắng 51.5%) rất uy tín.'),
(78, N'Bundesliga (Germany)', N'Germany', 2369, 3.24, 64.8, 23.5, 45.5, 9.80, 79.2, 45.0, 3.90, N'Gegenpressing, hàng thủ dâng cao: cỗ máy nổ Tài cả trận, nổ sớm H1 & bùng nổ cuối trận.', N'Giải đấu có TB bàn thắng cao nhất Top 5 Châu Âu (3.24 bàn/trận, 64.8% nổ Tài 2.5). Tới 79.2% trận có bàn Hiệp 1 và 45% trận có bàn sau phút 75 do lối đá pressing cường độ cao suốt 90 phút.'),
(13, N'CONMEBOL Libertadores (World)', N'World', 4102, 2.48, 46.5, 25.0, 54.5, 9.70, 65.0, 38.0, 5.60, N'Thánh địa sân nhà (độ cao/khán đài Nam Mỹ), thực dụng Xỉu bàn & mưa thẻ phạt.', N'Lợi thế sân nhà và độ cao (Bolivia, Ecuador, Colombia, Brazil) cực lớn giúp Chủ nhà thắng tới 54.5%; lối đá rát thực dụng ở cúp Nam Mỹ kéo TB bàn xuống 2.48 (53.5% Xỉu) nhưng TB tới 5.6 thẻ/trận.'),
(11, N'CONMEBOL Sudamericana (World)', N'World', 4100, 2.52, 48.0, 25.5, 51.5, 9.60, 66.0, 37.5, 5.40, N'Ưu thế tuyệt đối cho Chủ nhà, đá quyết liệt nhiều thẻ phạt & va chạm.', N'Chủ nhà thắng 51.5% nhờ khoảng cách địa lý xa xôi ở Nam Mỹ; trận đấu nhiều va chạm chiến thuật (TB 5.4 thẻ/trận), nhịp độ vừa phải (2.52 bàn/trận).'),
(40, N'Championship (England)', N'England', 8272, 2.66, 51.8, 26.5, 44.5, 10.70, 69.5, 41.0, 4.20, N'Thiên hướng Tài góc (TB 10.7 góc); lịch dày 46 vòng, trình độ đồng đều dễ lật kèo trên.', N'Lối đá tạt cánh đánh đầu và sút xa tạo TB 10.7 quả phạt góc/trận (top đầu Châu Âu). Mật độ 3 ngày/trận khiến các đội đuối sức cuối trận (41% bàn muộn) và cửa dưới rất hay thắng kèo.'),
(9, N'Copa America (World)', N'World', 12450, 2.18, 39.0, 28.5, 45.0, 8.60, 58.0, 34.0, 4.90, N'Đấu cúp ĐTQG cực kỳ chặt chẽ: chuyên Xỉu bàn, Xỉu góc, nhiều Hòa & mưa thẻ phạt.', N'Tính chất giải đấu loại trực tiếp ĐTQG Nam Mỹ khiến 61% trận về Xỉu 2.5 (TB chỉ 2.18 bàn và 8.6 góc/trận), tỷ lệ Hòa 90 phút cao (28.5%) và đá cực rát (4.9 thẻ/trận).'),
(308, N'Division 1 (Saudi-Arabia)', N'Saudi-Arabia', 8322, 2.44, 46.0, 27.5, 44.0, 9.20, 64.0, 36.0, 4.30, N'Giải hạng Nhất Ả Rập nhịp độ chậm: ưu tiên Xỉu bàn thắng, cửa dưới & kèo Hòa.', N'Khác với giải VĐQG Saudi Pro League nhiều siêu sao, giải Hạng Nhất có hàng công hạn chế (TB 2.44 bàn/trận, 54% về Xỉu 2.5) và tỷ lệ hòa/thắng sát nút cao (27.5% hòa).'),
(89, N'Eerste Divisie (Netherlands)', N'Netherlands', 2129, 3.28, 65.5, 22.0, 46.0, 10.50, 81.0, 46.0, 3.20, N'Các đội trẻ (Jong Ajax, PSV, AZ) đá cởi mở không toan tính: thiên đường Tài H1, Tài FT, Tài góc & bàn muộn.', N'TB 3.28 bàn/trận (65.5% nổ Tài 2.5), 81% số trận nổ bàn ngay Hiệp 1, TB 10.5 góc/trận và 46% trận có bàn sau phút 75 do các đội trẻ không chơi phòng ngự tiêu cực.'),
(88, N'Eredivisie (Netherlands)', N'Netherlands', 2385, 3.16, 62.0, 22.5, 47.5, 10.30, 77.5, 43.5, 3.10, N'Tấn công tổng lực 4-3-3, chênh lệch top đầu lớn: chuyên Tài bàn, Tài góc & tín kèo chấp sâu.', N'TB 3.16 bàn/trận (62% nổ Tài 2.5) và 10.3 góc/trận. Nhóm PSV, Feyenoord, Ajax vượt trội hoàn toàn phần còn lại nên thường xuyên thắng đậm kèo chấp sâu (-1.5, -2.0).'),
(98, N'J1 League (Japan)', N'Japan', 8256, 2.62, 50.5, 26.5, 42.0, 9.60, 67.0, 42.5, 2.80, N'Trình độ cực kỳ đồng đều, ít thẻ, hay nổ bàn sau phút 75 & cửa dưới rất hay ăn kèo.', N'Lợi thế sân nhà thấp (chỉ 42% thắng), trình độ các đội rất sát nhau nên kèo trên chấp từ -0.75 rất dễ gãy; thể lực bền bỉ giúp 42.5% số trận có bàn muộn sau phút 75.'),
(99, N'J2 League (Japan)', N'Japan', 8322, 2.45, 46.2, 28.5, 41.5, 9.30, 62.0, 38.5, 2.70, N'Nhập cuộc chặt chẽ, nhiều trận Hòa & cửa dưới uy tín: ưu tiên Xỉu bàn (đặc biệt H1) và nằm kèo dưới.', N'53.8% số trận về Xỉu 2.5 (TB 2.45 bàn/trận), tỷ lệ Hòa cao (28.5%) và chủ nhà chỉ thắng 41.5% -> Nằm cửa dưới được chấp +0.25/+0.5 cực kỳ an toàn.'),
(100, N'J3 League (Japan)', N'Japan', 8384, 2.54, 48.5, 27.5, 42.5, 9.20, 65.0, 39.0, 2.90, N'Trình độ sàn sàn nhau, phòng ngự lỏng cuối trận: ưu tiên kèo dưới được chấp & canh rung cuối trận.', N'Tỷ lệ hòa 27.5%, không có đội bóng quá vượt trội nên kèo chấp sâu rủi ro cao; hàng thủ hạng 3 hay mắc lỗi vị trí trong 15 phút cuối (39% bàn muộn).'),
(292, N'K League 1 (South-Korea)', N'South-Korea', 8256, 2.68, 51.5, 26.5, 43.5, 9.10, 67.5, 43.0, 3.90, N'Thể lực dồi dào, Hiệp 1 kín kẽ nhưng bùng nổ mạnh 20 phút cuối trận; trình độ đồng đều.', N'Các đội Hàn Quốc đá thể lực và kỷ luật trong Hiệp 1 nhưng đẩy cao tốc độ ở Hiệp 2 (43% trận có bàn sau phút 75); khoảng cách trình độ hẹp nên cửa dưới rất lì lợm.'),
(293, N'K League 2 (South-Korea)', N'South-Korea', 8256, 2.64, 50.8, 27.0, 43.0, 9.00, 66.5, 41.5, 4.10, N'Đá thực dụng Hiệp 1, hay có bàn muộn cuối trận & kèo dưới hay thắng kèo châu Á.', N'Chủ nhà chỉ thắng 43%, các đội hạng 2 Hàn Quốc chơi phòng ngự phản công rất rát khiến đội cửa trên khó thắng cách biệt; bàn thắng tập trung nhiều ở nửa cuối Hiệp 2.'),
(295, N'K3 League (South-Korea)', N'South-Korea', 8354, 2.42, 45.5, 28.0, 44.0, 8.60, 61.0, 35.0, 3.80, N'Giải bán chuyên Hàn Quốc nhịp độ thấp: thiên hướng Xỉu bàn, Xỉu góc & nhiều trận Hòa.', N'Hàng công hạn chế trong khâu dứt điểm khiến 54.5% số trận về Xỉu 2.5 (TB 2.42 bàn và 8.6 góc/trận), tỷ lệ hòa đạt 28%.'),
(1234, N'K4 League (South-Korea)', N'South-Korea', 2369, 2.98, 58.5, 22.5, 45.0, 8.90, 73.0, 42.0, 3.70, N'Hàng thủ nghiệp dư nhiều khoảng trống: chuyên nổ Tài bàn thắng & đuối sức cuối trận.', N'Hệ thống phòng ngự và thể lực của các đội K4 chênh lệch lớn dẫn tới TB 2.98 bàn/trận (58.5% nổ Tài 2.5) và nhiều bàn thua ở 15 phút cuối.'),
(140, N'La Liga (Spain)', N'Spain', 4224, 2.61, 48.8, 27.5, 46.5, 9.50, 66.5, 39.5, 4.90, N'Kỷ luật chiến thuật cao, tiểu xảo nhiều thẻ phạt; trừ Real/Barca thì phần còn lại đá chặt chẽ dễ Hòa.', N'Tỷ lệ nổ Tài chỉ 48.8% (TB 2.61 bàn/trận), tỷ lệ Hòa 27.5% và trọng tài Tây Ban Nha rút thẻ cực gắt (TB 4.9 thẻ/trận).'),
(274, N'Liga 1 (Indonesia)', N'Indonesia', 2117, 2.78, 54.2, 25.0, 51.5, 9.70, 72.0, 43.5, 4.60, N'Thánh địa sân nhà cuồng nhiệt; ngoại binh gánh hàng công còn hậu vệ nội hay hụt hơi cuối trận.', N'Chủ nhà thắng tới 51.5% nhờ áp lực khán giả và đi lại xa xôi giữa các đảo; thể lực giảm sút dưới thời tiết nóng ẩm khiến 43.5% trận có bàn sau phút 75.'),
(262, N'Liga MX (Mexico)', N'Mexico', 6229, 2.86, 55.8, 25.5, 48.0, 10.10, 73.5, 42.5, 4.80, N'Nhịp độ nhanh, sút xa & tạt biên nhiều: ưu tiên Tài bàn, Tài góc, Sân nhà & Tài thẻ.', N'TB 2.86 bàn/trận (55.8% nổ Tài 2.5), TB 10.1 góc/trận; lợi thế độ cao sân nhà rõ rệt (48% chủ nhà thắng) và VAR can thiệp nhiều (4.8 thẻ/trận).'),
(263, N'Liga de Expansión MX (Mexico)', N'Mexico', 12356, 2.74, 53.0, 27.0, 48.5, 9.80, 70.0, 40.5, 5.10, N'Mạnh sân nhà, đá rát nhiều thẻ phạt & cửa dưới hay gây bất ngờ.', N'Chủ nhà thắng 48.5%, đặc thù bóng đá hạng 2 Mexico va chạm quyết liệt (5.1 thẻ/trận) và không có đội xuống hạng nên lối đá cuối trận khá cởi mở (40.5% bàn muộn).'),
(61, N'Ligue 1 (France)', N'France', 65, 2.82, 54.8, 25.5, 45.0, 9.50, 71.5, 40.5, 3.90, N'Giàu tốc độ và chuyển trạng thái phản công nhanh: xu hướng nổ Tài bàn & bàn muộn Hiệp 2.', N'Từ khi rút xuống 18 đội, Ligue 1 đá tốc độ và trực diện hơn hẳn với TB 2.82 bàn/trận (54.8% nổ Tài 2.5) và 40.5% số trận có bàn sau phút 75.'),
(62, N'Ligue 2 (France)', N'France', 8322, 2.44, 45.5, 29.5, 43.5, 9.10, 62.5, 36.0, 3.80, N'Thực dụng kiểu Pháp: chuyên Xỉu bàn thắng, Vua hòa (29.5%) & cực tín cho cửa dưới.', N'54.5% số trận về Xỉu 2.5 (TB chỉ 2.44 bàn/trận); tỷ lệ Hòa thuộc nhóm cao nhất Châu Âu (29.5%) khiến các đội cửa trên chấp -0.5 rất hay thua kèo.'),
(253, N'Major League Soccer (USA)', N'USA', 10309, 3.12, 61.5, 24.5, 49.5, 9.90, 76.5, 44.5, 4.10, N'Quỹ lương ưu tiên tiền đạo ngôi sao còn hàng thủ yếu: cỗ máy nổ Tài, mạnh sân nhà & bàn muộn.', N'Quy tắc trần lương (DP) khiến các đội MLS dồn tiền mua siêu sao tấn công còn hậu vệ nội mắc nhiều lỗi -> TB 3.12 bàn/trận (61.5% Tài), đi khách bay xa qua nhiều múi giờ nên chủ nhà thắng 49.5%.'),
(39, N'Premier League (England)', N'England', 2129, 3.14, 61.8, 23.0, 46.0, 10.60, 76.5, 45.5, 4.30, N'Cường độ cao nhất thế giới & bù giờ dài (8-12 phút): chuyên Tài bàn, Tài góc & Bàn muộn.', N'TB 3.14 bàn/trận (61.8% nổ Tài 2.5), TB 10.6 góc/trận; quy định bù giờ dài tại Anh khiến tới 45.5% số trận có bàn thắng từ phút 75 trở đi.'),
(235, N'Premier League (Russia)', N'Russia', 4, 2.66, 51.0, 26.0, 47.5, 9.40, 68.0, 38.5, 4.30, N'Ưu thế sân nhà rõ rệt (khí hậu/di chuyển xa), mùa đông mặt sân xấu thiên về Xỉu.', N'Chủ nhà thắng 47.5% nhờ khoảng cách di chuyển xa và mặt cỏ; giai đoạn mùa thu-đông thời tiết lạnh khiến các trận đấu giữa bảng diễn ra giằng co.'),
(333, N'Premier League (Ukraine)', N'Ukraine', 258, 2.48, 47.0, 26.5, 43.5, 9.10, 63.5, 36.0, 4.20, N'Phân hóa mạnh giữa Shakhtar/Dynamo Kyiv và phần còn lại; nhóm dưới đá lùi sâu Xỉu bàn.', N'Hai ông lớn vượt trội hoàn toàn, trong khi các cặp đấu giữa những đội tầm trung diễn ra chặt chẽ, ít bàn thắng (53% về Xỉu 2.5, TB 2.48 bàn/trận).'),
(239, N'Primera A (Colombia)', N'Colombia', 4230, 2.26, 41.5, 30.5, 49.5, 9.10, 60.0, 35.5, 5.40, N'Thánh địa sân nhà (độ cao Bogota/Medellin), siêu Xỉu bàn thắng, Vua hòa & mưa thẻ phạt.', N'58.5% số trận về Xỉu 2.5 (TB chỉ 2.26 bàn/trận), tỷ lệ Hòa lên tới 30.5%, Chủ nhà thắng 49.5% nhờ chênh lệch độ cao địa hình và nhận TB 5.4 thẻ/trận.'),
(240, N'Primera B (Colombia)', N'Colombia', 12454, 2.34, 43.5, 29.0, 48.5, 8.90, 61.5, 36.0, 5.30, N'Thực dụng, nặng tính sân nhà, chuyên Xỉu bàn, nhiều Hòa & nhiều thẻ phạt.', N'TB 2.34 bàn/trận (56.5% Xỉu 2.5), tỷ lệ hòa 29% và lối đá cắt vụn trận đấu (5.3 thẻ/trận) giúp cửa dưới và kèo Xỉu rất sáng.'),
(162, N'Primera División (Costa Rica)', N'Costa-Rica', 4356, 2.68, 51.5, 26.5, 49.5, 9.50, 69.0, 39.0, 4.80, N'Mạnh sân nhà, nhóm Saprissa/Alajuelense/Herediano áp đảo & đá rát nhiều thẻ.', N'Chủ nhà thắng 49.5% (mặt sân nhân tạo/khí hậu từng vùng), nhóm Big 3 thống trị giải đấu và va chạm nhiều (TB 4.8 thẻ/trận).'),
(129, N'Primera Nacional (Argentina)', N'Argentina', 12454, 1.96, 32.5, 33.5, 46.5, 8.40, 53.0, 31.0, 5.10, N'Giải đấu Xỉu nhất thế giới (67.5% Xỉu 2.5): chuyên Xỉu bàn, Xỉu góc, Vua hòa (33.5%) & mưa thẻ.', N'TB chỉ 1.96 bàn/trận (67.5% về Xỉu 2.5), tỷ lệ Hòa 33.5% (cứ 3 trận có 1 trận hòa, cực nhiều tỷ số 0-0, 1-0, 1-1). Nằm cửa dưới +0.25/+0.5 và Xỉu là kim chỉ nam.'),
(307, N'Pro League (Saudi-Arabia)', N'Saudi-Arabia', 2369, 3.06, 59.5, 21.5, 46.5, 9.90, 75.0, 44.0, 4.20, N'Big 4 (Al Hilal, Al Nassr, Al Ittihad, Al Ahli) out trình: chuyên Tài bàn, Bàn muộn & tín kèo trên chấp sâu.', N'Sự chênh lệch khổng lồ giữa dàn siêu sao Châu Âu ở nhóm Big 4 với hàng thủ nội của các đội cuối bảng tạo TB 3.06 bàn/trận (59.5% Tài) và tỷ lệ thắng kèo chấp sâu rất cao.'),
(141, N'Segunda División (Spain)', N'Spain', 12418, 2.24, 39.8, 31.5, 46.0, 9.20, 58.5, 34.5, 5.00, N'Cực kỳ thực dụng: chuyên Xỉu bàn (60.2% Xỉu), Vua hòa (31.5%), tín cửa dưới & nhiều thẻ.', N'60.2% số trận về Xỉu 2.5 (TB 2.24 bàn/trận) và 31.5% số trận kết thúc Hòa; các đội dẫn 1 bàn là lập tức khóa chặt trận đấu bằng tiểu xảo (TB 5.0 thẻ/trận).'),
(71, N'Serie A (Brazil)', N'Brazil', 4246, 2.48, 47.2, 27.5, 51.0, 10.40, 64.5, 39.5, 5.20, N'Thánh địa sân nhà (thắng 51%), chuyên Tài góc (10.4 góc), thiên hướng Xỉu bàn & mưa thẻ phạt.', N'Lịch thi đấu dày và di chuyển xa giúp Chủ nhà thắng 51%; lối đá biên và sút xa tạo TB 10.4 góc/trận nhưng kết thúc trận chặt chẽ (52.8% Xỉu 2.5) và rất nóng đầu (5.2 thẻ/trận).'),
(135, N'Serie A (Italy)', N'Italy', 192, 2.66, 51.2, 27.0, 44.5, 9.60, 68.0, 40.0, 4.30, N'Nặng tính chiến thuật: Hiệp 1 thăm dò chặt chẽ, bàn thắng & đột biến dồn vào Hiệp 2.', N'Các đội bóng Ý tuân thủ chiến thuật rất kỹ trong 45 phút đầu nhưng khi bị dẫn bàn ở Hiệp 2 sẽ đẩy đội hình lên tạo nhiều khoảng trống (40% trận có bàn sau phút 75).'),
(72, N'Serie B (Brazil)', N'Brazil', 12438, 2.16, 37.5, 30.5, 50.5, 10.20, 57.0, 34.0, 5.30, N'Đặc sản Xỉu bàn thắng (62.5% Xỉu) nhưng nổ Tài góc (10.2 góc), cực mạnh sân nhà & nhiều Hòa.', N'Nghịch lý thú vị nhất cho dân soi kèo: Hàng công cùn (TB chỉ 2.16 bàn/trận, 62.5% Xỉu) nhưng cày góc cực khỏe (TB 10.2 góc/trận); chủ nhà bất bại gần 81% số trận (50.5% thắng + 30.5% hòa).'),
(278, N'Super League (Malaysia)', N'Malaysia', 2369, 3.32, 66.0, 19.5, 48.0, 9.60, 79.0, 44.0, 3.80, N'Johor Darul Ta''zim (JDT) và nhóm đầu out trình: siêu Tài bàn thắng, nổ sớm H1 & tín kèo chấp sâu.', N'Chênh lệch trình độ quá lớn giữa JDT/Selangor và các đội cuối bảng khiến tỷ số đậm 4-0, 5-1 xuất hiện liên tục (TB 3.32 bàn/trận, 66% nổ Tài 2.5).'),
(203, N'Süper Lig (Turkey)', N'Turkey', 6469, 2.96, 57.5, 24.5, 47.0, 9.80, 73.5, 43.5, 4.90, N'Galatasaray/Fenerbahce áp đảo, dàn tiền đạo ngôi sao Châu Âu: chuyên Tài bàn, Bàn muộn & nhiều thẻ.', N'Các CLB Thổ Nhĩ Kỳ chiêu mộ nhiều chân sút tên tuổi trong khi hàng thủ chậm chạp -> TB 2.96 bàn/trận (57.5% Tài), sức ép khán đài lớn và nhiều kịch tính phút bù giờ (43.5% bàn muộn).'),
(296, N'Thai League 1 (Thailand)', N'Thailand', 2373, 2.98, 58.2, 24.0, 48.0, 9.80, 74.0, 42.5, 4.20, N'Lối đá tấn công cởi mở, ngoại binh Brazil sắc bén: ưu tiên Tài bàn thắng, Tài H1 & Kèo trên.', N'Các CLB Thái Lan sử dụng bộ ba tiền đạo ngoại binh chất lượng cao đối đầu hậu vệ nội -> TB 2.98 bàn/trận (58.2% nổ Tài 2.5); nhóm Buriram, Bangkok Utd, Port FC thắng kèo trên rất đều.'),
(297, N'Thai League 2 (Thailand)', N'Thailand', 68, 2.76, 53.5, 25.5, 47.5, 9.50, 70.5, 41.0, 4.30, N'Điểm tựa sân nhà vững chắc & hàng thủ hay mất tập trung 15 phút cuối trận.', N'Chủ nhà thắng 47.5%, thể lực các đội hạng 2 Thái Lan suy giảm nhanh sau phút 70 tạo điều kiện cho ngoại binh ghi bàn muộn (41% trận có bàn sau phút 75).'),
(2, N'UEFA Champions League (World)', N'World', 2389, 3.12, 61.5, 21.0, 48.5, 10.10, 76.0, 43.5, 4.00, N'Thể thức Swiss Model cần đua hiệu số bàn thắng: chuyên Tài bàn, Tài góc & Kèo trên chấp sâu.', N'Thể thức League Phase 36 đội buộc các ông lớn phải ghi càng nhiều bàn càng tốt để đua hiệu số vào Top 8 -> TB 3.12 bàn/trận (61.5% nổ Tài 2.5), không còn cảnh thắng 1-0 rồi giữ sức.'),
(848, N'UEFA Europa Conference League (World)', N'World', 2373, 2.94, 57.2, 22.5, 49.0, 9.80, 73.0, 41.5, 4.20, N'Chênh lệch trình độ giữa các nền bóng đá lớn - nhỏ: ưu tiên Tài bàn, Chủ nhà & Kèo trên.', N'Sự góp mặt của nhiều đội bóng từ các nền bóng đá nhỏ có hàng thủ non kinh nghiệm giúp TB bàn đạt 2.94 bàn/trận (57.2% nổ Tài 2.5) và chủ nhà thắng 49%.'),
(3, N'UEFA Europa League (World)', N'World', 2117, 2.92, 56.8, 23.5, 48.0, 9.90, 72.5, 42.0, 4.30, N'Cởi mở và giàu tính cống hiến: thiên hướng nổ Tài bàn thắng, mạnh sân nhà & hay có bàn muộn.', N'TB 2.92 bàn/trận (56.8% nổ Tài 2.5); các đội tận dụng tối đa trận sân nhà tại League Phase (48% chủ nhà thắng) và thay người tạo đột biến cuối trận (42% bàn muộn).'),
(340, N'V.League 1 (Vietnam)', N'Vietnam', 68, 2.56, 49.5, 26.5, 48.5, 9.40, 65.5, 39.5, 4.10, N'Lợi thế sân nhà rõ rệt; nhóm đầu có tiền đạo ngoại nổ Tài, nhóm dưới đá rát thực dụng & dễ thủng lưới cuối trận.', N'Chủ nhà thắng 48.5% nhờ quen mặt cỏ và thời tiết vùng miền; Hiệp 1 thường nhập cuộc thăm dò nhưng 20 phút cuối thể lực nội binh đi xuống dễ xuất hiện bàn muộn (39.5%).');

BEGIN TRANSACTION;
EXEC sp_executesql N'
DECLARE @now datetime2 = SYSUTCDATETIME();
DECLARE @result TABLE (Action nvarchar(10));
MERGE dbo.LeagueDnas AS t
USING #src AS s ON t.LeagueId = s.LeagueId
WHEN MATCHED THEN UPDATE SET
        t.Tags = t.Tags | s.Tags,
        t.Country = s.Country,
        t.AvgGoals = s.AvgGoals,
        t.Over25Pct = s.Over25Pct,
        t.DrawPct = s.DrawPct,
        t.HomeWinPct = s.HomeWinPct,
        t.AvgCorners = s.AvgCorners,
        t.FirstHalfGoalPct = s.FirstHalfGoalPct,
        t.LateGoalPct = s.LateGoalPct,
        t.AvgCards = s.AvgCards,
        t.Summary = s.Summary,
        t.Analysis = s.Analysis,
        t.ProfileUpdatedAtUtc = @now
WHEN NOT MATCHED BY TARGET THEN
    INSERT (LeagueId, LeagueName, Tags, Notes, Country, AvgGoals, Over25Pct, DrawPct, HomeWinPct, AvgCorners, FirstHalfGoalPct, LateGoalPct, AvgCards, Summary, Analysis, ProfileUpdatedAtUtc)
    VALUES (s.LeagueId, s.LeagueName, s.Tags, NULL, s.Country, s.AvgGoals, s.Over25Pct, s.DrawPct, s.HomeWinPct, s.AvgCorners, s.FirstHalfGoalPct, s.LateGoalPct, s.AvgCards, s.Summary, s.Analysis, @now)
OUTPUT $action INTO @result;
SELECT
    SUM(CASE WHEN Action = ''INSERT'' THEN 1 ELSE 0 END) AS GiaiThemMoi,
    SUM(CASE WHEN Action = ''UPDATE'' THEN 1 ELSE 0 END) AS GiaiCapNhat
FROM @result;';
COMMIT TRANSACTION;

DROP TABLE #src;
