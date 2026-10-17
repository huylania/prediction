"""Gộp kết quả dna_from_api.py thành 1 script cập nhật database DNA (đội + giải) và 1 script khôi phục.

    python3 scripts/dna/gen_dna_update_sql.py [--dir scripts/dna/tu_api]

Đọc trong --dir các file do dna_from_api.py ghi (dna_doi_bong_30_giai_1nam_API.csv, team-ids_API.csv, dna_doi_bong_xoa.csv,
dna_giai_dau_46_giai_1nam_API.csv), rồi ghi:
  cap_nhat_dna_tu_api.sql      1. sao lưu TeamDnas, TeamDnaLeagues, LeagueDnas sang bảng *_SaoLuu_<ngày> (chỉ lần đầu)
                               2. đội: nhãn + số liệu = đúng như file, đồng bộ giải của đội, xoá DNA các đội trong dna_doi_bong_xoa.csv
                               3. giải: nhãn + số liệu = đúng như file
  khoi_phuc_dna_<ngày>.sql     đưa 3 bảng về đúng bản sao lưu (chạy khi muốn quay lại)
Ghi chú (Notes) của đội / giải luôn được giữ.
"""
import argparse
import datetime
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
TABLES = ["TeamDnas", "TeamDnaLeagues", "LeagueDnas"]


def generate(script: str, args: list[str]) -> str:
    """Chạy một generator sẵn có (gen_team_dna_sql.py / gen_league_dna_sql.py) và trả về nội dung SQL."""
    with tempfile.TemporaryDirectory() as tmp:
        out = Path(tmp) / "part.sql"
        subprocess.run([sys.executable, str(HERE / script), *args, "--output", str(out)], check=True)
        # Bỏ dòng "Cách chạy" của từng phần (trỏ tới file tạm); file gộp có hướng dẫn riêng ở đầu.
        return "\n".join(l for l in out.read_text(encoding="utf-8-sig").splitlines() if not l.startswith("-- Cách chạy")) + "\n"


def backup_sql(tag: str) -> str:
    lines = [
        "-- 1. Sao lưu 3 bảng DNA (chỉ tạo khi chưa có: chạy lại không ghi đè bản sao lưu đầu tiên).",
        "SET NOCOUNT ON;",
        "IF OBJECT_ID('dbo.TeamDnas') IS NULL OR OBJECT_ID('dbo.TeamDnaLeagues') IS NULL OR OBJECT_ID('dbo.LeagueDnas') IS NULL",
        "BEGIN",
        "    RAISERROR(N'CSDL chưa có bảng DNA. Hãy cập nhật và chạy website bản mới trước, rồi chạy lại script này.', 16, 1);",
        "    RETURN;",
        "END;",
    ]
    for table in TABLES:
        backup = f"{table}_SaoLuu_{tag}"
        lines += [
            f"IF OBJECT_ID('dbo.{backup}') IS NULL",
            "BEGIN",
            f"    SELECT * INTO dbo.{backup} FROM dbo.{table};",
            f"    PRINT N'Đã sao lưu dbo.{table} → dbo.{backup} (' + CAST(@@ROWCOUNT AS nvarchar(20)) + N' dòng).';",
            "END",
            "ELSE",
            f"    PRINT N'Đã có bản sao lưu dbo.{backup}: giữ nguyên bản đầu tiên.';",
        ]
    return "\n".join(lines)


def restore_sql(tag: str) -> str:
    backups = [f"dbo.{t}_SaoLuu_{tag}" for t in TABLES]
    lines = [
        f"-- Khôi phục DNA đội + giải về bản sao lưu ngày {tag} (do cap_nhat_dna_tu_api.sql tạo).",
        "-- Xoá dữ liệu hiện tại của dbo.TeamDnas, dbo.TeamDnaLeagues, dbo.LeagueDnas rồi chép lại từ bản sao lưu, trong 1 transaction.",
        "-- Bản sao lưu được giữ lại; muốn xoá thì chạy tay: DROP TABLE " + ", ".join(backups) + ";",
        "-- Cách chạy: SSMS / Azure Data Studio → Execute, hoặc sqlcmd -S <máy> -d <CSDL> -U <user> -P <mật khẩu> -f 65001 -i khoi_phuc_dna_" + tag + ".sql",
        "SET NOCOUNT ON;",
        "SET XACT_ABORT ON;",
        "IF " + " OR ".join(f"OBJECT_ID('{b}') IS NULL" for b in backups),
        "BEGIN",
        f"    RAISERROR(N'Không thấy bản sao lưu ngày {tag}. Kiểm tra lại tên bảng *_SaoLuu_{tag}.', 16, 1);",
        "    RETURN;",
        "END;",
        "",
        "DECLARE @cols nvarchar(max), @sql nvarchar(max);",
        "BEGIN TRANSACTION;",
        "DELETE FROM dbo.TeamDnaLeagues;",
        "DELETE FROM dbo.TeamDnas;",
        "DELETE FROM dbo.LeagueDnas;",
    ]
    # Chép theo các cột có ở cả bảng hiện tại lẫn bản sao lưu (an toàn khi website thêm cột sau này).
    for table in ["TeamDnas", "TeamDnaLeagues", "LeagueDnas"]:
        backup = f"{table}_SaoLuu_{tag}"
        lines += [
            "SELECT @cols = STRING_AGG(CAST(QUOTENAME(c.name) AS nvarchar(max)), N', ')",
            f"FROM sys.columns AS c WHERE c.object_id = OBJECT_ID('dbo.{table}') AND c.is_computed = 0",
            f"  AND EXISTS (SELECT 1 FROM sys.columns AS b WHERE b.object_id = OBJECT_ID('dbo.{backup}') AND b.name = c.name);",
            f"SET @sql = N'INSERT INTO dbo.{table} (' + @cols + N') SELECT ' + @cols + N' FROM dbo.{backup};';",
            "EXEC sp_executesql @sql;",
        ]
    lines += [
        "COMMIT TRANSACTION;",
        "",
        "SELECT",
        "    (SELECT COUNT(*) FROM dbo.TeamDnas) AS DoiDna,",
        "    (SELECT COUNT(*) FROM dbo.TeamDnaLeagues) AS DoiGiai,",
        "    (SELECT COUNT(*) FROM dbo.LeagueDnas) AS GiaiDna;",
    ]
    return "\n".join(lines) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--dir", type=Path, default=HERE / "tu_api", help="thư mục kết quả của dna_from_api.py")
    parser.add_argument("--date", default=datetime.date.today().strftime("%Y%m%d"), help="hậu tố bảng sao lưu (mặc định hôm nay)")
    args = parser.parse_args()
    d, tag = args.dir, args.date

    team_args = ["--source", str(d / "dna_doi_bong_30_giai_1nam_API.csv"), "--ids", str(d / "team-ids_API.csv"), "--replace"]
    if (d / "dna_doi_bong_xoa.csv").exists():
        team_args += ["--delete", str(d / "dna_doi_bong_xoa.csv")]
    team = generate("gen_team_dna_sql.py", team_args)
    league = generate("gen_league_dna_sql.py", ["--source", str(d / "dna_giai_dau_46_giai_1nam_API.csv"), "--replace"])

    header = [
        f"-- Cập nhật DNA đội + giải theo số liệu thật API-Football (scripts/dna/dna_from_api.py), sinh ngày {datetime.date.today().isoformat()}.",
        "-- Gồm 3 phần cách nhau bằng GO: (1) sao lưu 3 bảng DNA, (2) DNA đội, (3) DNA giải. Ghi chú (Notes) của bạn được giữ.",
        f"-- Muốn quay lại như trước khi chạy: chạy khoi_phuc_dna_{tag}.sql.",
        "-- Cách chạy: SSMS / Azure Data Studio → Execute (cả file), hoặc sqlcmd -S <máy> -d <CSDL> -U <user> -P <mật khẩu> -f 65001 -i cap_nhat_dna_tu_api.sql",
        "",
    ]
    body = "\n".join(header) + backup_sql(tag) + "\nGO\n\n-- 2. DNA đội\n" + team + "GO\n\n-- 3. DNA giải\n" + league + "GO\n"
    out = d / "cap_nhat_dna_tu_api.sql"
    out.write_text(body, encoding="utf-8-sig")
    restore = d / f"khoi_phuc_dna_{tag}.sql"
    restore.write_text(restore_sql(tag), encoding="utf-8-sig")
    print(f"Đã ghi {out} và {restore}")


if __name__ == "__main__":
    main()
