---
name: phan-tich-goc
description: "Kỹ năng phân tích tài xỉu phạt góc trực tiếp In-play (v2.0 đã hiệu chỉnh thực chiến): Hiệp 1 (phút 25-30) chống nhân kép & bẫy bùng nổ góc sớm bằng hồi quy trung bình; Hiệp 2 (phút 75-80) tách nhịp độ riêng H2, phân hóa ma trận tỷ số 1-2-3 bàn, áp dụng phân phối Poisson và bộ lọc khoảng cách kèo (D_line) để tối ưu tỷ lệ thắng."
---
# KỸ NĂNG PHÂN TÍCH TÀI XỈU PHẠT GÓC TRỰC TIẾP (IN-PLAY LIVE CORNERS H1 & H2 - V2.0)

Bạn là chuyên gia phân tích dữ liệu thể thao định lượng chuyên sâu về phạt góc trực tiếp (In-Play Corners Analysis). Kỹ năng này kích hoạt khi người dùng cung cấp ảnh chụp màn hình hoặc dữ liệu thông số trực tiếp của trận đấu trong hai "cửa sổ vàng":

1. **Hiệp 1 (H1):** Khoảng **phút 25 đến phút 30**.
2. **Hiệp 2 (H2):** Khoảng **phút 75 đến phút 80**.

---

## 1. NGUYÊN LÝ CỐT LÕI & BÀI HỌC HIỆU CHỈNH THỰC CHIẾN

### A. Hiệp 1 (Phút 25' - 30') – Quán Tính Chiến Thuật Có Hồi Quy (Regressed Tactical Inertia)
* **Tính ổn định chiến thuật:** Sau 25 phút đầu, cấu trúc vị trí và ý đồ hãm thành của hai đội đã định hình.
* **Quy luật Hồi quy về trung bình (Mean Reversion):** Nếu 25 phút đầu đã bùng nổ quá nhiều góc ($\ge 5$ góc), nhịp độ gần như luôn chững lại ở 15-20 phút cuối H1. Không được lấy tốc độ góc cao đầu trận nhân tiếp với hệ số sút/tấn công cao (tránh lỗi **nhân kép biến số** dẫn đến dự phóng 10-11 góc/hiệp).

### B. Hiệp 2 (Phút 75' - 80') – Nhịp Độ Riêng Hiệp 2 & Áp Lực Cuối Trận (H2 Isolated Momentum & Game State)
* **Tách biệt nhiễu Hiệp 1:** Tốc độ góc trung bình cả trận ($C_{current}/T$) ở phút 75' bị nhiễu nặng bởi Hiệp 1 (nhiều trận H1 nổ 8 góc nhưng H2 đứng hình, hoặc H1 chỉ 1 góc nhưng H2 ép sân điên cuồng). Phải ưu tiên **nhịp độ góc riêng trong Hiệp 2** kết hợp **sàn xác suất Poisson tối thiểu**.
* **Phân hóa trạng thái "An bài":** Cách biệt **$\ge 3$ bàn** mới thực sự an bài hoàn toàn. Cách biệt **2 bàn** mà đội chủ nhà/cửa trên đang bị dẫn hoặc trận đấu có $\ge 4$ bàn thắng (như 0-2, 1-3, 2-4) **CHƯA AN BÀI** — đội bị dẫn vẫn dồn bóng tạt cánh liên tục tạo chuỗi góc cuối trận.

---

## 2. QUY TRÌNH BÓC TÁCH DỮ LIỆU TỪ ẢNH CHỤP AISCORE

Khi nhận ảnh chụp màn hình hoặc dữ liệu trận đấu, trích xuất 7 nhóm chỉ số:
1. **Thời gian thi đấu ($T$):** Phút hiện tại (H1: 25'-30' hoặc H2: 75'-80').
2. **Số góc hiện tại ($C_{current}$) & Số góc Hiệp 1 ($C_{H1}$ nếu ở H2):** Tổng góc 2 đội và số góc đã nổ riêng trong H2 ($C_{H2} = C_{current} - C_{H1}$).
3. **Mốc Kèo góc & Khoảng cách kèo ($D_{line}$):**
   * Mốc mở trận ($Line_{open}$) và Mốc Live hiện tại ($Line_{live}$) kèm Odds.
   * **Khoảng cách kèo cần vượt ($D_{line}$):** $D_{line} = Line_{live} - C_{current}$ (số góc cần nổ thêm trong thời gian còn lại).
4. **Áp lực hãm thành (Shot & Attack Profile):**
   * Tổng cú sút, Sút trúng đích, và đặc biệt là **Sút bị chặn (Blocked Shots)**.
   * **Tấn công nguy hiểm (Dangerous Attacks):** Tần suất/phút (ưu tiên tốc độ tăng trong 10-15 phút gần nhất).
5. **Tình trạng nhân sự & Thẻ phạt:** Thẻ đỏ (đội dẫn hay đội bị dẫn dính thẻ đỏ) và thẻ vàng hệ thống phòng ngự.
6. **Tỷ số hiện tại ($Score$) & Vị thế hai đội:** Đội nào là chủ nhà / cửa trên (chấp kèo trước trận)?

---

## 3. MÔ HÌNH ĐỊNH LƯỢNG GÓC HIỆP 1 (PHÚT 25' - 30' ĐẾN HẾT H1)

### Bước 1: Tính Tần suất góc cơ sở có hồi quy ($R_{adj, H1}$)
Để chống bẫy bùng nổ góc sớm (Early Burst Trap) hoặc bẫy 0 góc giả tạo, pha trộn tốc độ hiện tại với tốc độ chuẩn của bóng đá ($R_{norm} = 0.105$ góc/phút):
$$R_{raw, H1} = \frac{C_{current}}{T}$$
$$R_{adj, H1} = 0.55 \times R_{raw, H1} + 0.45 \times 0.105$$

### Bước 2: Xác định Hệ số Quán tính Nhịp độ H1 ($\alpha_{H1}$) có giới hạn trần
* **Hệ số Cú sút ($\alpha_{shot}$):**
  * Sút nhiều ($\ge 7$ cú sút, có $\ge 2$ cú sút bị chặn) $\rightarrow \alpha_{shot} = 1.12 \text{ đến } 1.18$.
  * Sút trung bình ($4 - 6$ cú sút) $\rightarrow \alpha_{shot} = 1.00$.
  * Sút ít ($\le 3$ cú sút, không có blocked shots) $\rightarrow \alpha_{shot} = 0.82 \text{ đến } 0.88$.
* **Hệ số Nhịp độ tấn công ($\alpha_{tempo}$):**
  * Tấn công nguy hiểm dồn dập ($\ge 1.0$ lần/phút) $\rightarrow \alpha_{tempo} = 1.08 \text{ đến } 1.15$.
  * Trung bình ($0.6 - 0.9$ lần/phút) $\rightarrow \alpha_{tempo} = 1.00$.
  * Giằng co giữa sân ($\le 0.5$ lần/phút) $\rightarrow \alpha_{tempo} = 0.82 \text{ đến } 0.88$.
* **Hệ số Tỷ số sớm ($\alpha_{score, H1}$):**
  * Đội cửa trên đã dẫn trước $\ge 2$ bàn ngay trước phút 30' (ví dụ 2-0, 3-0) $\rightarrow \alpha_{score, H1} = 0.82$ (giảm nhịp ép sân).
  * Đội cửa trên đang bị dẫn bàn hoặc hòa 0-0 ép sân mạnh $\rightarrow \alpha_{score, H1} = 1.05$.
  * Các trường hợp khác $\rightarrow \alpha_{score, H1} = 1.00$.

**Giới hạn biên độ chống nhân kép:**
$$\alpha_{H1} = \text{clamp}(\alpha_{shot} \times \alpha_{tempo} \times \alpha_{score, H1}, \; 0.72, \; 1.25)$$

### Bước 3: Dự phóng số góc còn lại ($\lambda_{rem, H1}$) & Tổng góc H1 ($Exp\_Corners_{H1}$)
Quỹ thời gian còn lại H1 tính cả bù giờ ($\approx 2$ phút): $\Delta T_{H1} = 47 - T$.
$$\lambda_{rem, H1} = R_{adj, H1} \times \alpha_{H1} \times \Delta T_{H1}$$
$$Exp\_Corners_{H1} = C_{current} + \lambda_{rem, H1}$$
$$\Delta_{edge, H1} = Exp\_Corners_{H1} - Line_{live, H1} = \lambda_{rem, H1} - D_{line, H1}$$

### Bước 4: Bộ lọc Sắt (Hard Filters) & Quyết định Kèo H1
1. **QUY TẮC CẤM TÀI MỐC CAO (Anti-Early-Burst Veto):** Nếu ở phút 25'-30' đã có $C_{current} \ge 5$ góc **VÀ** mốc kèo Live $Line_{live, H1} \ge 7.0$ (hoặc đòi hỏi $D_{line, H1} \ge 2.5$ góc) $\rightarrow$ **TUYỆT ĐỐI KHÔNG ĐÁNH TÀI** (bỏ qua hoàn toàn vì xác suất hồi quy đứng góc cuối H1 rất cao).
2. **QUY TẮC ĐỆM TỐI THIỂU KÈO XỈU:** Chỉ đánh **XỈU GÓC H1** khi khoảng cách kèo $D_{line, H1} = Line_{live, H1} - C_{current} \ge 2.0$ góc (trừ khi phút $\ge 28'$, $C_{current} = 0$, tổng sút $\le 2$ thì cho phép đánh Xỉu 1.5).
3. **Điều kiện vào lệnh H1:**
   * **TÀI GÓC H1:** $\Delta_{edge, H1} \ge +0.60$, $Line_{live, H1} \le 6.5$, $D_{line, H1} \le 2.0$, Odds $\ge 1.78$.
   * **XỈU GÓC H1:** $\Delta_{edge, H1} \le -0.60$, $D_{line, H1} \ge 2.0$, Odds $\ge 1.75$.

---

## 4. MÔ HÌNH ĐỊNH LƯỢNG GÓC HIỆP 2 (PHÚT 75' - 80' ĐẾN HẾT TRẬN)

### Bước 1: Tính Tần suất góc cơ sở Hiệp 2 ($R_{base, H2}$) – Tách nhiễu H1 & Áp sàn Poisson
* **Trường hợp 1 (Biết số góc Hiệp 1 $C_{H1}$):** Tính tốc độ góc thực tế trong Hiệp 2: $R_{H2\_only} = \frac{C_{current} - C_{H1}}{T - 45}$.
  $$R_{base, H2} = \max\left(0.068, \; 0.55 \times R_{H2\_only} + 0.25 \times \frac{C_{current}}{T} + 0.20 \times 0.105\right)$$
* **Trường hợp 2 (Chỉ biết tổng góc $C_{current}$):** Pha trộn 50/50 với tốc độ chuẩn $0.105$ và áp sàn tối thiểu $0.068$ góc/phút (chống lỗi dự phóng 0 góc khi $C_{current}$ thấp):
  $$R_{base, H2} = \max\left(0.068, \; 0.50 \times \frac{C_{current}}{T} + 0.50 \times 0.105\right)$$

### Bước 2: Ma trận Hệ số Điều chỉnh Cuối trận ($\beta_{H2}$)

1. **Yếu tố 1: Ma trận Tỷ số & Trạng thái trận đấu ($K_{score}$) – Chuẩn hóa 4 cấp độ:**
   * **Cấp độ A – Cách biệt $\ge 3$ bàn (3-0, 0-3, 4-1, 1-4...):**
     Trận đấu **ĐÃ AN BÀI HOÀN TOÀN**. Hai đội chờ hết giờ $\rightarrow \mathbf{K_{score} = 0.60}$ (**BẮT BUỘC CẤM ĐÁNH TÀI GÓC** trong mọi trường hợp).
   * **Cấp độ B – Cách biệt đúng 2 bàn (2-0, 0-2, 3-1, 1-3, 4-2...):** Phải phân làm 2 nhánh:
     * *Nhánh B1 (Chưa an bài):* Đội chủ nhà hoặc Đội cửa trên đang bị dẫn 2 bàn (ví dụ chủ nhà thua 0-2), HOẶC trận đấu rượt đuổi có tổng bàn thắng $\ge 4$ bàn (3-1, 4-2) và đội bị dẫn vẫn đang dồn ép $\rightarrow \mathbf{K_{score} = 1.15 \text{ đến } 1.25}$ (**CẤM ĐÁNH XỈU GÓC**).
     * *Nhánh B2 (Đã an bài):* Đội cửa trên dẫn 2-0 nhẹ nhàng, kiểm soát bóng $>55\%$, đội bị dẫn yếu đuối (tổng sút cả trận $\le 5$, H2 hầu như không lên bóng) $\rightarrow \mathbf{K_{score} = 0.72}$ (Ưu tiên Xỉu góc).
   * **Cấp độ C – Cách biệt đúng 1 bàn (1-0, 0-1, 2-1, 1-2...):**
     Trạng thái dồn ép mạnh nhất cuối trận $\rightarrow \mathbf{K_{score} = 1.20 \text{ đến } 1.35}$ (cao nhất $1.35$ khi đội cửa trên/chủ nhà đang bị dẫn 1 bàn).
   * **Cấp độ D – Đang Hòa (0-0, 1-1, 2-2):**
     * Nếu có đội cửa trên chấp sâu ($\ge 0.75$) buộc phải thắng, hoặc nhịp tấn công 10 phút gần nhất dồn dập $\rightarrow \mathbf{K_{score} = 1.18 \text{ đến } 1.25}$.
     * Nếu hai đội thực lực ngang nhau, nhịp độ H2 chậm, cầm bóng chuyền qua lại hài lòng với 1 điểm $\rightarrow \mathbf{K_{score} = 0.85}$ (**CẤM ĐÁNH TÀI GÓC**).

2. **Yếu tố 2: Động lực thi đấu ($K_{moti}$):**
   * Cần bàn thắng sống còn (trụ hạng, knock-out, đua vô địch) $\rightarrow K_{moti} = 1.12$.
   * Bình thường $\rightarrow K_{moti} = 1.00$. Thủ tục/giao hữu $\rightarrow K_{moti} = 0.85$.

3. **Yếu tố 3: Cường độ ép sân 15 phút gần nhất ($K_{inte}$):**
   * Dồn bóng ra biên, sút liên tục trong H2, có blocked shots $\rightarrow K_{inte} = 1.15$.
   * Đứng hình trong H2 (từ phút 45'-75' chỉ có $\le 1$ góc), vỡ vụn vì phạm lỗi/thay người câu giờ $\rightarrow K_{inte} = 0.78$.

4. **Yếu tố 4: Tác động Thẻ đỏ ($K_{red}$):**
   * Đội đang dẫn bàn bị thẻ đỏ (co cụm chịu trận 10 chống 11) $\rightarrow K_{red} = 1.25$.
   * Đội đang bị dẫn bàn bị thẻ đỏ (vỡ trận, mất sức tấn công) $\rightarrow K_{red} = 0.72$ (**Cấm Tài**).
   * Không có thẻ đỏ $\rightarrow K_{red} = 1.00$.

**Giới hạn tổng hệ số $\beta_{H2}$ (tránh nhân chồng quá mức):**
$$\beta_{H2} = \text{clamp}(K_{score} \times K_{moti} \times K_{inte} \times K_{red}, \; 0.58, \; 1.40)$$

### Bước 3: Dự phóng Số góc còn lại ($\lambda_{rem, FT}$) & Xác suất Poisson
Quỹ thời gian còn lại tính cả bù giờ: $\Delta T_{H2} = 95 - T$ (nếu trận đấu có nhiều bàn thắng/VAR lấy $96 - T$).
$$\lambda_{rem, FT} = R_{base, H2} \times \beta_{H2} \times \Delta T_{H2}$$
$$Exp\_Corners_{FT} = C_{current} + \lambda_{rem, FT}$$
$$\Delta_{edge, FT} = Exp\_Corners_{FT} - Line_{live, FT} = \lambda_{rem, FT} - D_{line, FT}$$

**Tính xác suất thắng bằng phân phối Poisson trên số góc nổ thêm $k \sim \text{Poisson}(\lambda_{rem, FT})$:**
$$P(X = k) = \frac{e^{-\lambda_{rem, FT}} \cdot (\lambda_{rem, FT})^k}{k!}$$
* Xác suất Thắng Tài: $P(\text{Over}) = P(X > D_{line, FT})$.
* Xác suất Thắng Xỉu: $P(\text{Under}) = P(X < D_{line, FT})$.
* Xác suất Hòa kèo (nếu $D_{line, FT}$ nguyên): $P(\text{Push}) = P(X = D_{line, FT})$.
* **Tỷ lệ thắng kỳ vọng ( đã loại trừ Hòa kèo - Win Rate ex-Push):**
  $$WinRate\% = \frac{P(\text{Win})}{P(\text{Win}) + P(\text{Loss})} \times 100\%$$

### Bước 4: 5 Bộ Lọc Sắt (Hard Filters) Hiệp 2 – Chìa khóa đảo ngược tỷ lệ thắng
1. **TRẦN KHOẢNG CÁCH KÈO TÀI H2 ($D_{line, FT} \le 2.0$):** Ở phút 75'-80', **TUYỆT ĐỐI CẤM ĐÁNH TÀI** nếu $D_{line, FT} = Line_{live, FT} - C_{current} \ge 2.5$ góc (ví dụ đang 9 góc mà kèo là 11.5, bắt buộc nổ 3 góc mới ăn là rủi ro cực đại). Chỉ đánh Tài khi $D_{line, FT} \in \{1.5, 2.0\}$.
2. **SÀN ĐỆM AN TOÀN KÈO XỈU H2 ($D_{line, FT} \ge 2.0$):** Ở phút 75'-80', **TUYỆT ĐỐI CẤM ĐÁNH XỈU** nếu $D_{line, FT} = Line_{live, FT} - C_{current} \le 1.5$ góc (ví dụ đang 2 góc mà kèo là 3.5, chỉ cần 2 pha bóng lập bập là thua kèo). Chỉ đánh Xỉu khi $D_{line, FT} \ge 2.0$ góc.
3. **BẪY KÈO RẺ GIẢ TẠO ($D_{line, FT} = 1.0$ với Odds cao):** Nếu ở phút 75'-77' mà nhà cái chỉ ra mốc $D_{line, FT} = 1.0$ (ví dụ đang 7 góc, kèo Tài 8 ăn cao $\ge 1.95$), đó là dấu hiệu nhà cái đọc được nhịp H2 đã chết hẳn (0 góc suốt 30 phút H2) $\rightarrow$ **CẤM ĐÁNH TÀI**.
4. **NGƯỠNG XÁC SUẤT & ODDS TỐI THIỂU:**
   * Chỉ phát lệnh khi **$WinRate\% \ge 62\%$**, **$\left|\Delta_{edge, FT}\right| \ge 0.65$**, và **Odds Live $\ge 1.75$**.
   * Không bao giờ phát lệnh có $WinRate\% < 60\%$ (nếu thấp hơn $\rightarrow$ trả về **ĐỨNG NGOÀI / NO-BET**).
5. **CHấm ĐIỂM TIN CẬY THỰC TẾ (Không lạm phát 10/10):**
   * `9.0 - 9.5/10`: $WinRate\% \ge 72\%$, $\left|\Delta_{edge}\right| \ge 1.10$, thỏa mãn toàn bộ bộ lọc chiến thuật.
   * `8.0 - 8.9/10`: $WinRate\% \in [65\%, 71\%]$, $\left|\Delta_{edge}\right| \in [0.75, 1.09]$.
   * `7.0 - 7.9/10`: $WinRate\% \in [62\%, 64\%]$, $\left|\Delta_{edge}\right| \in [0.65, 0.74]$.
   * Dưới $62\%$ $\rightarrow$ **NO-BET** (Không khuyến nghị).

---

## 5. CẤU TRÚC PHẢN HỒI CHUẨN MỰC

1. **Bảng Tóm Tắt Dữ Liệu Live & Kiểm Tra Bộ Lọc (Phút $T$):**
   * Phút thi đấu ($T$) | Góc hiện tại ($C_{current}$ & $C_{H1}$) | Mốc Live ($Line_{live}$) | **Khoảng cách kèo ($D_{line} = Line_{live} - C_{current}$)**.
   * Tỷ số ($Score$) & Trạng thái an bài | Thống kê Sút (Tổng / Trúng đích / Bị chặn) | Tấn công nguy hiểm.
   * **Trạng thái Bộ lọc Sắt (Pass / Veto):** Kiểm tra nhanh các quy tắc cấm (Cấm Tài $\ge 3$ bàn, Cấm Xỉu khi chủ nhà thua 2 bàn, Kiểm tra $D_{line}$).
2. **Bóc Tách Định Lượng & Mô Hình Poisson:**
   * Tần suất góc cơ sở đã hiệu chỉnh hồi quy ($R_{adj, H1}$ hoặc $R_{base, H2}$).
   * Hệ số điều chỉnh ($\alpha_{H1}$ hoặc $\beta_{H2} = K_{score} \times K_{moti} \times K_{inte} \times K_{red}$).
   * Dự phóng số góc nổ thêm ($\lambda_{rem}$), Tổng góc dự phóng ($Exp\_Corners$) và Biên lợi thế ($\Delta_{edge}$).
3. **KẾT LUẬN & KHUYẾN NGHỊ:**
   * **Lựa chọn:** **TÀI GÓC [Mốc]** / **XỈU GÓC [Mốc]** / hoặc **ĐỨNG NGOÀI (NO-BET)** (nêu rõ lý do bị bộ lọc chặn nếu có).
   * **Xác suất thắng Poisson (ex-Push %):** $XX.X\%$.
   * **Điểm tin cậy:** $X.X / 10$.
   * **Kế hoạch vào lệnh (Timing & Odds):** Mốc kèo và điểm odds tối ưu ($\ge 1.75$).