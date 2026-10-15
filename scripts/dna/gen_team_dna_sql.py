"""Sinh script SQL nạp DNA đội bóng (file 19 giải, 1 năm) vào bảng dbo.TeamDnas.

    python3 scripts/dna/gen_team_dna_sql.py

Đọc dna_doi_bong_19_giai_1nam.csv + team-ids.csv (id API-Football, tạo bằng resolve_team_ids.py)
và ghi team_dna_19_giai.sql. Mỗi đội 1 dòng: đội có nhiều dòng (vd giải quốc nội + cúp châu lục)
lấy dòng nhiều trận nhất, bằng nhau thì ưu tiên giải quốc nội.
"""
import csv
import datetime
from decimal import Decimal, InvalidOperation
from pathlib import Path

HERE = Path(__file__).resolve().parent
SOURCE = HERE / "dna_doi_bong_19_giai_1nam.csv"
IDS = HERE / "team-ids.csv"
OUTPUT = HERE / "team_dna_19_giai.sql"

# Cột tích trong CSV → giá trị cờ DnaTag (src/Prediction.Core/Entities/Dna.cs).
TAGS = {
    "Co_May_No_Tai": 1,  # OverGoals
    "Manh_San_Nha": 4,  # HomeFortress
    "Yeu_San_Khach": 8,  # AwayFragile
    "Cay_Goc": 16,  # OverCorners
    "Bung_No_Cuoi_Tran_H2": 64,  # LateGoals
    "Thuc_Dung_Pha_Loi_Choi": 128,  # Pragmatic
    "Bat_Nat_Doi_Yeu_Chap_Sau": 256,  # BullyWeak
    "Cong_Lam_Thu_Pha_BTTS": 512,  # Btts
    "Xa_Goc_Chiu_Goc": 1024,  # ConcedesCorners
    "Danh_Phu_Dau_Tai_H1": 2048,  # FastStart
    "Da_Rat_Cay_The": 4096,  # HeavyCards
}

# Cột số liệu CSV → cột bảng TeamDnas.
STATS = [
    ("TB_Tong_Ban", "AvgTotalGoals"),
    ("TB_Ban_Ghi", "AvgGoalsFor"),
    ("TB_Ban_Thua", "AvgGoalsAgainst"),
    ("Ty_Le_Tai_2_5_Pct", "Over25Pct"),
    ("Ty_Le_BTTS_Pct", "BttsPct"),
    ("Thang_San_Nha_Pct", "HomeWinPct"),
    ("Thua_San_Khach_Pct", "AwayLossPct"),
    ("TB_Goc_Tu_Kiem", "AvgCornersFor"),
    ("TB_Goc_Phai_Chiu", "AvgCornersAgainst"),
    ("Ty_Le_Co_Ban_H1_Pct", "FirstHalfGoalPct"),
    ("TB_Ban_H2", "AvgSecondHalfGoals"),
    ("TB_The_Phat", "AvgCards"),
]

CUPS = {2, 17, 18}  # C1 châu Âu, C1/C2 châu Á: chỉ dùng khi đội không có dòng giải quốc nội nhiều trận hơn


def sql_str(text: str | None, limit: int) -> str:
    if text is None or text.strip() == "":
        return "NULL"
    return "N'" + text.strip()[:limit].replace("'", "''") + "'"


def sql_dec(text: str, places: int) -> str:
    try:
        return str(round(Decimal(text), places))
    except (InvalidOperation, ValueError):
        return "NULL"


def league_name(raw: str) -> str:
    # "Bundesliga (Germany)" → "Bundesliga (Germany)" (giữ nguyên, tối đa 100 ký tự)
    return raw.strip()[:100]


def main() -> None:
    rows = list(csv.DictReader(SOURCE.open(encoding="utf-8-sig")))
    ids = {(r["League_Id"], r["Doi_Bong"]): r for r in csv.DictReader(IDS.open(encoding="utf-8"))}

    best: dict[int, dict] = {}
    names: dict[int, str] = {}
    skipped = []
    for r in rows:
        m = ids.get((r["League_Id"], r["Doi_Bong"]))
        if not m or not m["TeamId"]:
            skipped.append(f"{r['League_Id']} | {r['Doi_Bong']}")
            continue
        team_id = int(m["TeamId"])
        names.setdefault(team_id, m["Api_Name"] or r["Doi_Bong"])
        key = (int(r["So_Tran_1_Nam"]), int(r["League_Id"]) not in CUPS)
        current = best.get(team_id)
        if current is None or key > (int(current["So_Tran_1_Nam"]), int(current["League_Id"]) not in CUPS):
            best[team_id] = r

    values = []
    for team_id, r in sorted(best.items(), key=lambda x: (x[1]["Giai_Dau"], names[x[0]])):
        tags = sum(flag for col, flag in TAGS.items() if r[col].strip() == "1")
        stats = ", ".join(sql_dec(r[col], 2 if not col.endswith("Pct") else 1) for col, _ in STATS)
        values.append(
            f"({team_id}, {sql_str(names[team_id], 200)}, {tags}, {int(r['League_Id'])}, {sql_str(league_name(r['Giai_Dau']), 100)}, "
            f"{int(r['So_Tran_1_Nam'])}, {stats}, {sql_str(r['Ghi_Chu'], 500)}, {sql_str(r['Phan_Tich_Ly_Do_Chon_1_Nam'], 2000)})")

    stat_cols = ", ".join(c for _, c in STATS)
    today = datetime.date.today().isoformat()
    lines = [
        "-- DNA đội bóng 19 giải (số liệu 1 năm) → dbo.TeamDnas",
        f"-- Sinh tự động bởi scripts/dna/gen_team_dna_sql.py ngày {today}: {len(values)} đội từ {len(rows)} dòng CSV.",
        "-- CHẠY SAU KHI website bản mới đã khởi động ít nhất 1 lần (migration TeamDnaProfile thêm các cột hồ sơ).",
        "-- Đội đã có DNA: giữ các tích cũ + cộng thêm tích từ file (phép OR), giữ Tên và Ghi chú; ghi đè số liệu 1 năm.",
        "-- Đội chưa có: thêm mới. Chạy lại nhiều lần vẫn cho cùng kết quả.",
        "-- Cách chạy: mở bằng SSMS / Azure Data Studio rồi Execute, hoặc: sqlcmd -S <máy> -d <CSDL> -U <user> -P <mật khẩu> -f 65001 -i team_dna_19_giai.sql",
        "SET NOCOUNT ON;",
        "SET XACT_ABORT ON;",
        "",
        "-- Kiểm tra cột mới; thiếu thì dừng (MERGE chạy bằng sp_executesql nên không báo lỗi tên cột lẫn vào).",
        "IF COL_LENGTH('dbo.TeamDnas', 'Analysis') IS NULL",
        "BEGIN",
        "    RAISERROR(N'Bảng TeamDnas chưa có cột hồ sơ 1 năm. Hãy cập nhật và chạy website bản mới trước (migration TeamDnaProfile), rồi chạy lại script này.', 16, 1);",
        "    RETURN;",
        "END;",
        "",
        "IF OBJECT_ID('tempdb..#src') IS NOT NULL DROP TABLE #src;",
        "CREATE TABLE #src (",
        "    TeamId int NOT NULL PRIMARY KEY, TeamName nvarchar(200) NOT NULL, Tags int NOT NULL,",
        "    SourceLeagueId int NULL, SourceLeagueName nvarchar(100) NULL, Matches int NULL,",
        "    AvgTotalGoals decimal(5,2) NULL, AvgGoalsFor decimal(5,2) NULL, AvgGoalsAgainst decimal(5,2) NULL,",
        "    Over25Pct decimal(5,1) NULL, BttsPct decimal(5,1) NULL, HomeWinPct decimal(5,1) NULL, AwayLossPct decimal(5,1) NULL,",
        "    AvgCornersFor decimal(5,2) NULL, AvgCornersAgainst decimal(5,2) NULL, FirstHalfGoalPct decimal(5,1) NULL,",
        "    AvgSecondHalfGoals decimal(5,2) NULL, AvgCards decimal(5,2) NULL,",
        "    Summary nvarchar(500) NULL, Analysis nvarchar(2000) NULL);",
        "",
    ]
    # INSERT ... VALUES tối đa 1000 dòng mỗi lệnh.
    for i in range(0, len(values), 200):
        lines.append(f"INSERT INTO #src (TeamId, TeamName, Tags, SourceLeagueId, SourceLeagueName, Matches, {stat_cols}, Summary, Analysis) VALUES")
        lines.append(",\n".join(values[i:i + 200]) + ";")
        lines.append("")

    set_stats = ",\n        ".join(f"t.{c} = s.{c}" for _, c in STATS)
    merge = [
        "DECLARE @now datetime2 = SYSUTCDATETIME();",
        "DECLARE @result TABLE (Action nvarchar(10));",
        "MERGE dbo.TeamDnas AS t",
        "USING #src AS s ON t.TeamId = s.TeamId",
        "WHEN MATCHED THEN UPDATE SET",
        "        t.Tags = t.Tags | s.Tags,",
        "        t.SourceLeagueId = s.SourceLeagueId,",
        "        t.SourceLeagueName = s.SourceLeagueName,",
        "        t.Matches = s.Matches,",
        f"        {set_stats},",
        "        t.Summary = s.Summary,",
        "        t.Analysis = s.Analysis,",
        "        t.ProfileUpdatedAtUtc = @now",
        "WHEN NOT MATCHED BY TARGET THEN",
        f"    INSERT (TeamId, TeamName, Tags, Notes, SourceLeagueId, SourceLeagueName, Matches, {stat_cols}, Summary, Analysis, ProfileUpdatedAtUtc)",
        f"    VALUES (s.TeamId, s.TeamName, s.Tags, NULL, s.SourceLeagueId, s.SourceLeagueName, s.Matches, {', '.join('s.' + c for _, c in STATS)}, s.Summary, s.Analysis, @now)",
        "OUTPUT $action INTO @result;",
        "SELECT",
        "    SUM(CASE WHEN Action = 'INSERT' THEN 1 ELSE 0 END) AS DoiThemMoi,",
        "    SUM(CASE WHEN Action = 'UPDATE' THEN 1 ELSE 0 END) AS DoiCapNhat",
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
    if skipped:
        lines += ["", "-- Các dòng CSV chưa có id đội (sửa team-ids.csv rồi sinh lại):"] + [f"--   {s}" for s in skipped]

    OUTPUT.write_text("\n".join(lines) + "\n", encoding="utf-8-sig")
    print(f"Đã ghi {OUTPUT.name}: {len(values)} đội ({len(rows)} dòng CSV, {len(skipped)} dòng thiếu id).")


if __name__ == "__main__":
    main()
