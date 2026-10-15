"""Sinh script SQL nạp DNA giải đấu (file 46 giải, 1 năm) vào bảng dbo.LeagueDnas.

    python3 scripts/dna/gen_league_dna_sql.py

Đọc dna_giai_dau_46_giai_1nam.csv (League_Id là id API-Football) và ghi league_dna_46_giai.sql.
Giải đã có DNA: giữ tích cũ + cộng thêm tích từ file (phép OR), giữ Tên và Ghi chú; ghi đè hồ sơ 1 năm.
"""
import csv
import datetime
from decimal import Decimal, InvalidOperation
from pathlib import Path

HERE = Path(__file__).resolve().parent
SOURCE = HERE / "dna_giai_dau_46_giai_1nam.csv"
OUTPUT = HERE / "league_dna_46_giai.sql"

# Cột tích trong CSV → giá trị cờ DnaTag (src/Prediction.Core/Entities/Dna.cs, nhãn dạng giải).
TAGS = {
    "Tai_Ban_Thang": 1,  # OverGoals
    "Xiu_Ban_Thang": 2,  # UnderGoals
    "Thanh_Dia_San_Nha": 4,  # HomeFortress
    "Tai_Goc": 16,  # OverCorners
    "Xiu_Goc": 32,  # UnderCorners
    "Ban_Muon_Duoi_Suc_Cuoi_Tran": 64,  # LateGoals
    "Vua_Hoa_Thang_Sat_Nut": 128,  # Pragmatic (giải: vua hòa / thắng sát nút)
    "Phan_Hoa_Lon_Tin_Keo_Tren": 256,  # BullyWeak (giải: phân hóa lớn, tín kèo trên)
    "No_Som_Hiep_1": 2048,  # FastStart (giải: nổ sớm hiệp 1)
    "Mua_The_De_The_Do_Pen": 4096,  # HeavyCards (giải: mưa thẻ)
    "Cua_Duoi_Hay_An_Dong_Deu": 8192,  # UnderdogCover
}

# Cột số liệu CSV → (cột bảng LeagueDnas, số chữ số thập phân).
STATS = [
    ("TB_Ban_Moi_Tran", "AvgGoals", 2),
    ("Ty_Le_Tai_2_5_Pct", "Over25Pct", 1),
    ("Ty_Le_Hoa_Pct", "DrawPct", 1),
    ("Ty_Le_Chu_Nha_Thang_Pct", "HomeWinPct", 1),
    ("TB_Tong_Goc", "AvgCorners", 2),
    ("Ty_Le_Co_Ban_H1_Pct", "FirstHalfGoalPct", 1),
    ("Ty_Le_Ban_Sau_Phut_75_Pct", "LateGoalPct", 1),
    ("TB_The_Vang_Do", "AvgCards", 2),
]


def sql_str(text: str | None, limit: int) -> str:
    if text is None or text.strip() == "":
        return "NULL"
    return "N'" + text.strip()[:limit].replace("'", "''") + "'"


def sql_dec(text: str, places: int) -> str:
    try:
        return str(round(Decimal(text), places))
    except (InvalidOperation, ValueError):
        return "NULL"


def main() -> None:
    rows = list(csv.DictReader(SOURCE.open(encoding="utf-8-sig")))
    seen: set[int] = set()
    values = []
    for r in sorted(rows, key=lambda x: x["Giai_Dau"]):
        league_id = int(r["League_Id"])
        if league_id in seen:
            continue
        seen.add(league_id)
        tags = sum(flag for col, flag in TAGS.items() if r[col].strip() == "1")
        stats = ", ".join(sql_dec(r[col], places) for col, _, places in STATS)
        values.append(
            f"({league_id}, {sql_str(r['Giai_Dau'], 200)}, {sql_str(r['Quoc_Gia'], 100)}, {tags}, {stats}, "
            f"{sql_str(r['Ghi_Chu'], 500)}, {sql_str(r['Phan_Tich_Ly_Do_Chon_1_Nam'], 2000)})")

    stat_cols = ", ".join(c for _, c, _ in STATS)
    today = datetime.date.today().isoformat()
    lines = [
        f"-- DNA giải đấu {len(values)} giải (số liệu 1 năm) → dbo.LeagueDnas",
        f"-- Sinh tự động bởi scripts/dna/gen_league_dna_sql.py ngày {today} từ {SOURCE.name}.",
        "-- CHẠY SAU KHI website bản mới đã khởi động ít nhất 1 lần (migration LeagueDnaProfile thêm cột hồ sơ, bật theo dõi các giải mới).",
        "-- Giải đã có DNA: giữ các tích cũ + cộng thêm tích từ file (phép OR), giữ Tên và Ghi chú; ghi đè hồ sơ 1 năm.",
        "-- Giải chưa có: thêm mới. Chạy lại nhiều lần vẫn cho cùng kết quả.",
        f"-- Cách chạy: mở bằng SSMS / Azure Data Studio rồi Execute, hoặc: sqlcmd -S <máy> -d <CSDL> -U <user> -P <mật khẩu> -f 65001 -i {OUTPUT.name}",
        "SET NOCOUNT ON;",
        "SET XACT_ABORT ON;",
        "",
        "-- Kiểm tra cột mới; thiếu thì dừng (MERGE chạy bằng sp_executesql nên không báo lỗi tên cột lẫn vào).",
        "IF COL_LENGTH('dbo.LeagueDnas', 'Analysis') IS NULL",
        "BEGIN",
        "    RAISERROR(N'Bảng LeagueDnas chưa có cột hồ sơ 1 năm. Hãy cập nhật và chạy website bản mới trước (migration LeagueDnaProfile), rồi chạy lại script này.', 16, 1);",
        "    RETURN;",
        "END;",
        "",
        "IF OBJECT_ID('tempdb..#src') IS NOT NULL DROP TABLE #src;",
        "CREATE TABLE #src (",
        "    LeagueId int NOT NULL PRIMARY KEY, LeagueName nvarchar(200) NOT NULL, Country nvarchar(100) NULL, Tags int NOT NULL,",
        "    AvgGoals decimal(5,2) NULL, Over25Pct decimal(5,1) NULL, DrawPct decimal(5,1) NULL, HomeWinPct decimal(5,1) NULL,",
        "    AvgCorners decimal(5,2) NULL, FirstHalfGoalPct decimal(5,1) NULL, LateGoalPct decimal(5,1) NULL, AvgCards decimal(5,2) NULL,",
        "    Summary nvarchar(500) NULL, Analysis nvarchar(2000) NULL);",
        "",
        f"INSERT INTO #src (LeagueId, LeagueName, Country, Tags, {stat_cols}, Summary, Analysis) VALUES",
        ",\n".join(values) + ";",
        "",
    ]

    set_stats = ",\n        ".join(f"t.{c} = s.{c}" for _, c, _ in STATS)
    merge = [
        "DECLARE @now datetime2 = SYSUTCDATETIME();",
        "DECLARE @result TABLE (Action nvarchar(10));",
        "MERGE dbo.LeagueDnas AS t",
        "USING #src AS s ON t.LeagueId = s.LeagueId",
        "WHEN MATCHED THEN UPDATE SET",
        "        t.Tags = t.Tags | s.Tags,",
        "        t.Country = s.Country,",
        f"        {set_stats},",
        "        t.Summary = s.Summary,",
        "        t.Analysis = s.Analysis,",
        "        t.ProfileUpdatedAtUtc = @now",
        "WHEN NOT MATCHED BY TARGET THEN",
        f"    INSERT (LeagueId, LeagueName, Tags, Notes, Country, {stat_cols}, Summary, Analysis, ProfileUpdatedAtUtc)",
        f"    VALUES (s.LeagueId, s.LeagueName, s.Tags, NULL, s.Country, {', '.join('s.' + c for _, c, _ in STATS)}, s.Summary, s.Analysis, @now)",
        "OUTPUT $action INTO @result;",
        "SELECT",
        "    SUM(CASE WHEN Action = 'INSERT' THEN 1 ELSE 0 END) AS GiaiThemMoi,",
        "    SUM(CASE WHEN Action = 'UPDATE' THEN 1 ELSE 0 END) AS GiaiCapNhat",
        "FROM @result;",
    ]
    lines += [
        "BEGIN TRANSACTION;",
        "EXEC sp_executesql N'",
        "\n".join(merge).replace("'", "''") + "';",
        "COMMIT TRANSACTION;",
        "",
        "DROP TABLE #src;",
    ]

    OUTPUT.write_text("\n".join(lines) + "\n", encoding="utf-8-sig")
    print(f"Đã ghi {OUTPUT.name}: {len(values)} giải ({len(rows)} dòng CSV).")


if __name__ == "__main__":
    main()
