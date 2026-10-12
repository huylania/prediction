# Soi kèo cá nhân — Blazor .NET + SQL Server

Website biến **kinh nghiệm bet của bạn thành quy tắc**, ví dụ *"MU gặp đội xếp dưới trên BXH → chọn MU"*. Website sẽ:

- **Gợi ý** các trận sắp đá khớp quy tắc, với mức kèo và odds hiện tại.
- **Tư vấn** từng trận: gộp các quy tắc khớp (kèm thành tích của từng quy tắc), **biến động kèo** chấp và tài xỉu, cùng dự đoán của API-Football. Website đếm tín hiệu ủng hộ/ngược lại và cảnh báo khi chúng mâu thuẫn.
- **Kiểm chứng** quy tắc trên dữ liệu cũ: tỉ lệ thắng kèo ở từng mức chấp hoặc tài xỉu, và lãi/lỗ theo kèo thật.
- **Chấm thành tích thực tế**: mỗi gợi ý được chốt theo kèo đóng lúc bóng lăn, rồi chấm thắng, thắng nửa, hòa, thua nửa hoặc thua khi có kết quả.

Hỗ trợ **kèo chấp châu Á** và **tài xỉu**, kể cả các mức 1/4 và 3/4.

> Đây là công cụ tham khảo, không đảm bảo lợi nhuận.

## Yêu cầu

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server: LocalDB (có sẵn khi cài Visual Studio), Express, Developer, hoặc chạy bằng Docker.
- API key của [API-Football](https://www.api-football.com/) (api-sports.io v3). Website lấy kèo và tư vấn khá dày, khoảng 1.000–2.000 request/ngày vào cuối tuần, nên cần **gói Pro (7.500 request/ngày) trở lên**.

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

# 3) Chạy. CSDL được tạo/cập nhật tự động khi khởi động.
dotnet run
```

Mở `http://localhost:5245`. Lần đầu vào, website yêu cầu **tạo tài khoản quản trị**. Đây là tài khoản duy nhất, dùng để đăng nhập các lần sau.

Chạy SQL Server bằng Docker, nếu chưa có:

```bash
docker run -d --name mssql -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=MatKhau!Manh123" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```

Thay vì user-secrets, bạn có thể dùng biến môi trường: `ConnectionStrings__Default`, `ApiFootball__ApiKey`.

### Xem thử khi chưa có API key

```bash
dotnet run -- --seed-demo
```

Lệnh này tạo **"Giải Demo"** gồm 12 đội giả, 2 mùa kết quả, cùng kèo biến động và tư vấn **giả lập**, kèm 3 quy tắc mẫu. Bạn cũng có thể tạo hoặc xóa dữ liệu demo bằng nút trong **Cài đặt**. Đây không phải dữ liệu thật.

## Việc cần làm sau lần chạy đầu

1. **Cài đặt → Đồng bộ ngay → "Giải đấu"**: xác minh id các giải với API.
   - Id đã có sẵn: Premier League 39, La Liga 140, Serie A 135, Bundesliga 78, Ligue 1 61, Champions League 2, V.League 1 340.
   - Nếu nhật ký báo "Không tìm thấy" (nhất là V-League), tìm giải bằng ô **"Tìm và thêm giải"** (vd gõ `Vietnam`), thêm giải đúng và bỏ theo dõi giải sai.
2. **"Tải mùa cũ"** 3–5 mùa gần nhất cho mỗi giải (mỗi mùa tốn 1 request). Phần kiểm chứng không cần kèo dùng các kết quả này.
3. Mở trang chi tiết một trận sắp đá và **đối chiếu kèo website đọc được** với kèo trên trang của nhà cái.
   - Cách API-Football ghi mức chấp ("Home -0.75 / Away +0.75" hay "Home -0.75 / Away -0.75") chưa được kiểm tra với API thật lúc viết phần mềm.
   - Bộ đọc kèo xử lý được cả hai kiểu, nhưng bạn nên kiểm tra một lần.
4. Sửa quy tắc mẫu *"MU gặp đội xếp dưới → chọn MU"* cho đúng thói quen của bạn, rồi bấm **Kiểm chứng**.

Sau đó website tự đồng bộ ở chế độ nền:

| Dữ liệu | Chu kỳ mặc định | Ghi chú |
|---|---|---|
| Lịch & kết quả | 2 giờ | 1 request/giải |
| Kèo theo giải | 1 giờ | bắt kèo mở và biến động trong ngày |
| Kèo sát giờ | 15 phút | từng trận, trong 3 giờ trước giờ bóng lăn → kèo đóng |
| Tư vấn API | 6 giờ + 1 lần ~1 giờ trước trận | các trận trong 3 ngày tới |
| Gợi ý | 5 phút | sinh, chốt và chấm gợi ý (không tốn request) |

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

## Giới hạn cần biết

- BXH tự tính theo điểm → hiệu số → bàn thắng. Không tính trừ điểm hành chính hay tiêu chí đối đầu riêng của từng giải.
- Champions League chỉ có thứ hạng ở **league phase** (từ mùa 2024/25). Ở vòng knock-out và vòng bảng kiểu cũ, các điều kiện về thứ hạng không áp dụng.
- Kèo chấm theo tỉ số **90 phút**, không tính hiệp phụ và luân lưu.
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
src/Prediction.Web/         Blazor Web App (Interactive Server), EF Core SQL Server
  ApiFootball/              Client, DTO, bộ đọc kèo, ánh xạ dữ liệu
  Services/                 Đồng bộ nền, gợi ý, tư vấn, kiểm chứng, dữ liệu demo, tài khoản
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
