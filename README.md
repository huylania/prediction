# Soi kèo cá nhân — Blazor .NET + SQL Server

Website biến **kinh nghiệm bet của bạn thành quy tắc**, ví dụ *"MU gặp đội xếp dưới trên BXH → chọn MU"*. Website sẽ:

- **Gợi ý** các trận sắp đá khớp quy tắc, với mức kèo và odds hiện tại.
- **Trang Trận đấu** kiểu tỉ số trực tiếp (Đang đá / Sắp đá / Kết quả): dưới mỗi trận có ô gợi ý **kèo chấp châu Á** và **tài xỉu**, trận đang đá cập nhật phút và tỉ số mỗi phút.
- **Tư vấn** từng trận: gộp các quy tắc khớp (kèm thành tích của từng quy tắc), **biến động kèo** chấp và tài xỉu, cùng dự đoán của API-Football. Website đếm tín hiệu ủng hộ/ngược lại và cảnh báo khi chúng mâu thuẫn.
- **Kiểm chứng** quy tắc trên dữ liệu cũ: tỉ lệ thắng kèo ở từng mức chấp hoặc tài xỉu, và lãi/lỗ theo kèo thật.
- **Chấm thành tích thực tế**: mỗi gợi ý được chốt theo kèo đóng lúc bóng lăn, rồi chấm thắng, thắng nửa, hòa, thua nửa hoặc thua khi có kết quả.
- **Phân tích chuyên sâu** theo file kỹ năng `src/Prediction.Web/Ai/bong-da-skill.md`: DNA giải/đội, quy tắc chờ ổn định 2–3 trận khi đổi HLV hoặc có tân binh, kèo chấp dự phóng và kèo lạ, 5 cờ đỏ, đối chiếu 3 lớp bàn thắng / phạt góc / thẻ, xác suất và thang điểm 10 chia **Nhóm A / B / C**.
- **Cảnh báo In-play** cho trận đang đá theo 5 quy tắc của file kỹ năng, dựa trên thống kê trực tiếp (sút, góc, thẻ, phút ghi bàn).
- **Báo cáo AI** (tùy chọn): Claude đọc toàn bộ số liệu phân tích, tự tra cứu web và viết báo cáo đúng cấu trúc mục 9 của file kỹ năng.

Hỗ trợ **kèo chấp châu Á** và **tài xỉu** (kể cả các mức 1/4 và 3/4); phần phân tích chuyên sâu đọc thêm **tài xỉu góc, chấp góc và tài xỉu thẻ** của Bet365.

> Đây là công cụ tham khảo, không đảm bảo lợi nhuận.

## Yêu cầu

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server: LocalDB (có sẵn khi cài Visual Studio), Express, Developer, hoặc chạy bằng Docker.
- API key của [API-Football](https://www.api-football.com/) (api-sports.io v3). Với 15 giải mặc định, website dùng khoảng **4.000–5.500 request vào ngày cuối tuần** (kèo, tỉ số + thống kê trực tiếp, chấn thương, HLV, tải dần thống kê lịch sử), nên cần **gói Pro (7.500 request/ngày) trở lên**. Khi còn dưới 1.000 request trong ngày, các tác vụ phụ tự tạm dừng.
- (Tùy chọn) API key Claude từ [console.anthropic.com](https://console.anthropic.com/) nếu muốn dùng báo cáo AI.

## Cài đặt và chạy

```bash
git clone <repo> && cd prediction

# 1) Chuỗi kết nối SQL Server.
#    Mặc định (appsettings.json) dùng LocalDB: Server=(localdb)\MSSQLLocalDB;Database=Prediction;...
#    Nếu dùng SQL Server khác:
cd src/Prediction.Web
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=Prediction;User Id=sa;Password=MAT_KHAU;TrustServerCertificate=True"

# 2) API key của API-Football (không ghi vào appsettings.json)
dotnet user-secrets set "ApiFootball:ApiKey" "KEY_CUA_BAN"

# 3) (Tùy chọn) API key Claude cho báo cáo AI
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."

# 4) Chạy. CSDL được tạo/cập nhật tự động khi khởi động.
dotnet run
```

Mở `http://localhost:5245`. Lần đầu vào, website yêu cầu **tạo tài khoản quản trị**. Đây là tài khoản duy nhất, dùng để đăng nhập các lần sau.

Chạy SQL Server bằng Docker, nếu chưa có:

```bash
docker run -d --name mssql -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=MatKhau!Manh123" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```

Thay vì user-secrets, bạn có thể dùng biến môi trường: `ConnectionStrings__Default`, `ApiFootball__ApiKey`, `Anthropic__ApiKey`.

### Xem thử khi chưa có API key

```bash
dotnet run -- --seed-demo
```

Lệnh này tạo **"Giải Demo"** gồm 12 đội giả, 2 mùa kết quả, cùng kèo biến động và tư vấn **giả lập**, kèm 3 quy tắc mẫu. Bạn cũng có thể tạo hoặc xóa dữ liệu demo bằng nút trong **Cài đặt**. Đây không phải dữ liệu thật.

## Việc cần làm sau lần chạy đầu

1. Website tự đồng bộ ngay khi khởi động. Id các giải đã được kiểm tra với API thật: Premier League 39, La Liga 140, Serie A 135, Bundesliga 78, Ligue 1 61, Champions League 2, V.League 1 340, 2. Bundesliga 79, Eredivisie 88, Eerste Divisie 89, MLS 253, Brazil Serie A 71, Brazil Serie B 72, Ligue 2 62, Segunda División 141. Muốn theo dõi thêm giải khác, dùng ô **"Tìm và thêm giải"** trong Cài đặt; bỏ tick giải không cần để tiết kiệm request.
2. **"Tải mùa cũ"** 3–5 mùa gần nhất cho mỗi giải (mỗi mùa tốn 1 request). Phần kiểm chứng không cần kèo dùng các kết quả này.
   - Số mùa nhập theo cách API đánh số: phần lớn giải ghi theo năm bắt đầu (2025 = mùa 2025/26), riêng **V-League ghi theo năm kết thúc** (2026 = mùa 2025/26). Website tự hiện nhãn mùa đúng.
3. Sửa quy tắc mẫu *"MU gặp đội xếp dưới → chọn MU"* cho đúng thói quen của bạn, rồi bấm **Kiểm chứng**.

Về dữ liệu kèo: API-Football ghi kèo chấp kiểu "cùng số" (`Home -0.75 @1.95` / `Away -0.75 @1.90` là một cặp, con số là mức chấp của đội nhà). Website lấy **mức chính** là cặp có hai odds gần nhau nhất, giống mức nhà cái hiển thị đầu tiên. Cách đọc này đã kiểm tra với dữ liệu Bet365 thật.

Website tự đồng bộ ở chế độ nền theo chu kỳ:

| Dữ liệu | Chu kỳ mặc định | Ghi chú |
|---|---|---|
| Lịch & kết quả | 2 giờ | 1 request/giải |
| Tỉ số & thống kê trực tiếp | 2 phút | chỉ khi có trận đang đá; 1 request / 20 trận |
| Kèo theo giải | 1 giờ | chấp, tài xỉu, góc, chấp góc, thẻ; bắt kèo mở và biến động |
| Kèo sát giờ | 15 phút | từng trận, trong 3 giờ trước giờ bóng lăn → kèo đóng |
| Tư vấn API | 6 giờ + 1 lần ~1 giờ trước trận | các trận trong 3 ngày tới |
| Thống kê trận đã đá | 30 phút, tối đa 20 request/lần | góc, thẻ, sút, xG, phút ghi bàn; mùa này + mùa trước, trận gần nhất trước. Lần đầu tải dần trong ~12 giờ |
| Chấn thương | 6 giờ | các trận trong 24 giờ tới (giải có hỗ trợ) |
| HLV | 1 ngày | các đội có trận trong 3 ngày tới, mỗi đội 1 lần/tuần |
| Gợi ý | 5 phút | sinh, chốt và chấm gợi ý (không tốn request) |
| Phân tích chuyên sâu | 10 phút | cập nhật khuyến nghị A/B/C, chốt lúc bóng lăn, chấm sau trận (không tốn request) |

Chỉnh chu kỳ trong `appsettings.json`, mục `Sync`. Nhà cái mặc định là Bet365 (`ApiFootball:BookmakerId = 8`).

## Viết quy tắc

Mỗi quy tắc là một bộ lọc. Các điều kiện đều **tùy chọn**: để trống nghĩa là không xét, và mọi điều kiện đã điền phải **cùng đúng**.

- **Đội A** là đội bạn đang xét, **Đội B** là đối thủ. Mỗi trận được xét từ cả hai phía (A = đội nhà, rồi A = đội khách).
- **Mức chấp ghi theo phía đội A**: âm = A chấp, dương = A được chấp. Ví dụ "A chấp từ 1/4 đến 1 trái" ghi là từ `-1` đến `-0.25`.

| Nhóm | Điều kiện |
|---|---|
| Đội, BXH & phong độ | đội A cụ thể, sân nhà/khách, A xếp trên B ≥/≤ N bậc, A trong top N, B từ hạng N trở xuống, A hơn B ≥ N điểm, điểm 5 trận gần nhất, trung bình tổng bàn 5 trận gần nhất, số trận tối thiểu đã đá |
| Kèo hiện tại | khoảng mức chấp của A, khoảng mức tài xỉu, odds tối thiểu |
| Biến động kèo | kèo chấp nghiêng về A / về B / đứng yên; tài xỉu nghiêng Tài / Xỉu / đứng yên; ngưỡng dịch mức kèo và ngưỡng giảm tiền kèo |
| Tư vấn API | API dự đoán A thắng, % thắng của A ≥ N, API dự đoán tài/xỉu cùng hướng |

Ví dụ:

- **MU gặp đội xếp dưới → chọn MU**: Đội A = Manchester United, A xếp trên B ít nhất 1 bậc, Lựa chọn = Chọn đội A (kèo chấp).
- **Chỉ theo MU khi tiền đổ về MU và MU chấp không quá 1 trái**: thêm "Kèo chấp nghiêng về A" và mức chấp của A từ `-1` đến `0`.
- **Hai đội đá cởi mở → Tài**: Loại kèo = Tài xỉu, Lựa chọn = Tài, TB tổng bàn 2 đội từ `3`.

### Cách đọc biến động kèo

So sánh **kèo mở** (lần đầu website thấy kèo) với **kèo hiện tại**, hoặc **kèo đóng** nếu trận đã bắt đầu.

- **Kèo chấp nghiêng về một đội** khi:
  - mức chấp dịch về phía đội đó, tức đội đó phải chấp nhiều hơn hoặc được chấp ít hơn ("kèo tăng" cho đội đó). Ngưỡng mặc định là 1/4 trái; hoặc
  - mức chấp đứng yên nhưng tiền kèo đội đó giảm ít nhất 0.05.
- **Tài xỉu nghiêng về Tài** khi mức tài xỉu tăng, hoặc mức đứng yên nhưng tiền Tài giảm.

Website chỉ lưu một bản kèo mới khi mức hoặc tiền kèo thay đổi. Vì vậy trang chi tiết trận cho thấy **toàn bộ lịch sử biến động**, có biểu đồ và bảng từng lần đổi.

### Kiểm chứng

- **Phần 1 – Thống kê kết quả (không cần kèo)**:
  - Chỉ xét điều kiện đội, BXH và phong độ, trên mọi mùa đã tải.
  - BXH được tính lại **tại thời điểm trước mỗi trận**, nên không "nhìn trước" kết quả.
  - Kết quả gồm tỉ lệ thắng/hòa/thua, phân bố hiệu số, và tỉ lệ thắng kèo nếu luôn đánh ở từng mức chấp −2 … +2 hoặc tài xỉu 1.5 … 4.5, kèm **odds hòa vốn**.
- **Phần 2 – Lợi nhuận theo kèo thật**: xét đủ mọi điều kiện, chấm theo kèo đóng của các trận có kèo. Kết quả gồm số kèo, tỉ lệ thắng, lãi/lỗ, ROI, chuỗi thua dài nhất, mức sụt giảm lớn nhất và biểu đồ lãi lũy kế.

Lưu ý: API-Football chỉ giữ kèo khoảng 7 ngày, nên **kèo, biến động và tư vấn API chỉ tích lũy từ lúc website bắt đầu chạy**. Với trận cũ, bạn có thể **nhập kèo tay** ở trang chi tiết trận. Kèo có thời điểm trước giờ bóng lăn được dùng làm kèo đóng.

## Phân tích chuyên sâu (theo file kỹ năng)

Trang **Phân tích chuyên sâu** chia các trận trong 1–3 ngày tới thành 3 cột: **Nhóm A** (8.0–10, khả dụng trước trận), **Nhóm B** (6.0–7.9, chỉ vào In-play sau phút 15–20) và **Nhóm C** (dưới 6 hoặc dính cờ đỏ). Bên dưới là thành tích các khuyến nghị A/B đã chốt. Tab **Phân tích chuyên sâu** ở trang chi tiết trận trình bày đủ 6 mục của file kỹ năng.

Cách website tính (các ngưỡng lấy nguyên từ file kỹ năng):

- **Mẫu phong độ**: 10 trận gần nhất mọi giải có trong CSDL. Nếu đội có HLV mới hoặc tân binh trụ cột: trận 1–3 → **cờ đỏ 5, chờ ổn định**; từ trận 4 → chỉ dùng các trận dưới giai đoạn mới. Mẫu dưới 5 trận được kéo về trung bình giải.
- **Bàn thắng**: `Kỳ vọng = ((Ghi nhà + Thủng khách) + (Ghi khách + Thủng nhà)) / 2`. Tài khi Δ ≥ +0.40 và kỳ vọng ≥ trung bình giải; Xỉu ngược lại.
- **Kèo chấp dự phóng**: độ vượt trội đội nhà = 0.6 × phong độ + 0.4 × (chênh hiệu số/trận ÷ 2) + lợi thế sân (0.30; giải "thánh địa sân nhà" 0.60; đội nhà "mạnh sân nhà" +0.25; đội khách "yếu sân khách" +0.25), làm tròn về bội số 1/4. `Δ_H = kèo nhà cái − dự phóng`; |Δ_H| ≥ 0.5 hoặc kèo nhảy ≥ 0.5 → **kèo lạ**, website tìm nguyên nhân (chấn thương, lịch dày, HLV/tân binh, DNA sân) để phán "kèo thực lực" hay "nghi bẫy".
- **Phạt góc** (không xét dòng tiền): kỳ vọng tương tự bàn thắng, trộn số liệu chung với số liệu theo sân; Tài/Xỉu góc khi |Δ| ≥ 1.2 kèm phong cách; chấp góc khi chênh góc kiếm ≥ 2.0/trận và mức chấp nhà cái thấp hơn. Thẻ chỉ để tham khảo (tối đa nhóm B).
- **Xác suất**: Poisson cho bàn, nhị thức âm cho góc/thẻ; tính thắng / thắng nửa / hoàn / thua nửa / thua cho đúng mức kèo thật và EV theo odds.
- **Điểm** = 5 + phần xác suất + phần EV + đạt ngưỡng (+0.75) + DNA cùng/ngược hướng (±0.5–1.0) + đã qua giai đoạn ổn định (+0.5) + API cùng hướng (+0.25) + quy tắc của bạn cùng hướng (+0.5) + kèo dịch về cửa đó (+0.25) − mẫu ít trận.

Trang **DNA & nhân sự** cho sửa nhãn DNA của giải/đội (đã nhập sẵn theo file kỹ năng) và nhập tay HLV mới / tân binh trụ cột. HLV mới được lấy tự động từ API nhưng dữ liệu API có thể thiếu, nên hãy kiểm tra lại.

**Cảnh báo In-play** hiện trên thẻ trận đang đá: bàn siêu sớm (≤ 15'), hạ nhịp săn Tài ở trận DNA Tài có mốc ≥ 3.25, kiểm chứng HLV/tân binh mới ở phút 20–25, bế tắc hiệp 1 (0-0, sút trúng đích ≤ 1), bàn muộn ở giải đuối sức cuối trận. Website không lấy kèo live (tiết kiệm request), nên cảnh báo ghi mốc kèo để bạn tự đối chiếu.

### Báo cáo AI (Claude)

Ở cuối tab Phân tích chuyên sâu, bấm **"Viết báo cáo AI"**. Website gửi cho Claude (model `claude-opus-5-5`, effort `high`):

- **System prompt** là nguyên file kỹ năng, được cache 1 giờ (các báo cáo sau trong 1 giờ rẻ hơn).
- **Tin nhắn** là toàn bộ số liệu phân tích của trận (JSON). Claude được phép tra cứu web tối đa 5 lượt để kiểm tra tân binh, HLV, đội hình, chấn thương.
- Bật sẵn **fallback khi bị từ chối** (`fallbacks: "default"`): nếu bộ lọc an toàn từ chối, API tự chạy lại bằng model dự phòng.

Báo cáo hiện dần trên trang, được lưu lại (bấm **"Phân tích lại"** để viết bản mới). Chi phí ước tính **$0.2–0.5/lượt** (hiện kèm mỗi báo cáo). Cấu hình trong `appsettings.json`, mục `Anthropic` (`Model`, `Effort`, `MaxTokens`, `WebSearch`, `WebSearchMaxUses`).

## Giới hạn cần biết

- BXH tự tính theo điểm → hiệu số → bàn thắng. Không tính trừ điểm hành chính hay tiêu chí đối đầu riêng của từng giải.
- Champions League chỉ có thứ hạng ở **league phase** (từ mùa 2024/25). Ở vòng knock-out và vòng bảng kiểu cũ, các điều kiện về thứ hạng không áp dụng.
- Kèo chấm theo tỉ số **90 phút**, không tính hiệp phụ và luân lưu.
- Phân tích chuyên sâu chỉ thấy được những gì có trong dữ liệu: chấn thương, lịch thi đấu, HLV/tân binh (API + nhập tay), DNA. Đội hình xoay tua, động lực, nội bộ, trọng tài cần bạn tự kiểm tra (hoặc dùng báo cáo AI để tra cứu).
- Thống kê góc/thẻ của mùa cũ được tải dần; vài ngày đầu số liệu góc của các giải ít dữ liệu có thể còn thiếu. Một số giải không có thống kê trận trên API (vd V-League) nên không có phân tích góc/thẻ.
- Quy tắc kèo chấp không có điều kiện phân biệt đội A, ví dụ chỉ "chọn đội A khi chấp 1/4", sẽ không có thống kê phần 1. Nếu ở một trận cả hai phía cùng khớp và chọn hai cửa ngược nhau, trận đó bị bỏ qua.

## Cấu trúc mã nguồn

```
src/Prediction.Core/        Logic thuần (không phụ thuộc CSDL/HTTP)
  Entities/                 League, Team, Fixture, OddsSnapshot, ApiAdvice, Rule, Suggestion, ...
  Engine/Settlement.cs      Chấm kèo chấp/tài xỉu (1/4, 3/4)
  Engine/Standings.cs       BXH & phong độ trước mỗi trận
  Engine/OddsMovement.cs    Biến động kèo, hướng nghiêng, mô tả bằng lời
  Engine/RuleEvaluator.cs   Xét quy tắc
  Engine/MatchAdvisor.cs    Tư vấn tổng hợp
  Engine/BacktestRunner.cs  Kiểm chứng
  Engine/Analysis/          Phân tích chuyên sâu: MatchAnalyzer, phân phối xác suất, mẫu phong độ, InPlayEngine
src/Prediction.Web/         Blazor Web App (Interactive Server), EF Core SQL Server
  ApiFootball/              Client, DTO, bộ đọc kèo, ánh xạ dữ liệu
  Services/                 Đồng bộ nền, gợi ý, tư vấn, kiểm chứng, phân tích chuyên sâu, báo cáo AI, dữ liệu demo, tài khoản
  Ai/bong-da-skill.md       File kỹ năng (system prompt của báo cáo AI)
  Components/Pages/         Các trang
  Data/                     DbContext, seed, migrations
tests/Prediction.Tests/     xUnit
```

Lệnh hữu ích:

```bash
dotnet test                                    # chạy test
dotnet tool restore                            # cài dotnet-ef theo manifest
dotnet ef migrations add TenMigration -p src/Prediction.Web -o Data/Migrations
```
