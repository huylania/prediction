"""Tính lại DNA đội bóng (30 giải) và DNA giải đấu (46 giải) từ các trận đã đá trên API-Football trong 1 năm qua.

Gọi API cho từng giải:
  leagues?id=<giải>                     các mùa nằm trong khung 1 năm, độ phủ thống kê
  fixtures?league=<giải>&season=<mùa>   danh sách trận, tỉ số, tỉ số hiệp 1
  fixtures?ids=a-b-...  (20 trận/lần)   thống kê góc, thẻ, diễn biến bàn thắng
Khoảng 1.000 request cho cả hai file. Phản hồi lưu trong --cache: chạy lại cùng ngày không tốn request,
thống kê trận đã đá giữ luôn cho các lần sau.

Công thức và ngưỡng chép đúng từ src/Prediction.Core/Engine/Analysis/DnaVerifier.cs. Cột nhãn (0/1):
số liệu đạt ngưỡng "khớp" → 1, không đạt → 0. Giữ giá trị file cũ (kèm ghi chú) khi chưa đủ trận, API không có
số liệu góc/thẻ, hoặc nhãn cần dữ liệu kèo (Bắt nạt đội yếu chấp sâu, Cửa dưới hay ăn đồng đều, Phân hoá lớn tín kèo trên).

Ghi 4 file vào --out:
  dna_doi_bong_30_giai_1nam_API.csv    cùng cột với file đội, số liệu + nhãn tính từ API (thay thẳng file cũ được)
  dna_doi_bong_doi_chieu.csv           từng đội: tên file ↔ tên API, số trận, nhãn file → API, cảnh báo
  dna_giai_dau_46_giai_1nam_API.csv    cùng cột với file giải, tính từ API
  dna_giai_dau_doi_chieu.csv           đối chiếu từng giải

    APIFOOTBALL_KEY=... python3 scripts/dna/dna_from_api.py [--out thư_mục] [--cache thư_mục] [--end 2026-10-17]
"""
import argparse
import csv
import datetime as dt
import json
import os
import sys
import time
import urllib.error
import urllib.request
from collections import defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
TEAM_FILE = HERE / "dna_doi_bong_30_giai_1nam.csv"
LEAGUE_FILE = HERE / "dna_giai_dau_46_giai_1nam.csv"
IDS_FILE = HERE / "team-ids.csv"
BASE = "https://v3.football.api-sports.io/"
FINISHED = {"FT", "AET", "PEN"}
MIN_REMAINING = 2000  # còn ít hơn chừng này request trong ngày thì dừng, chừa quota cho website

# (nhãn, chỉ số, số trận tối thiểu, ngưỡng khớp, ngưỡng ngược, tên chỉ số, là %) — giống TeamRules trong DnaVerifier.cs.
# Chỉ số None: cần dữ liệu kèo, không kiểm được bằng tỉ số.
TEAM_RULES = {
    "Co_May_No_Tai": ("Cỗ máy nổ Tài", "over25", 10, lambda v: v >= 55, lambda v: v < 45, "Tài 2.5", True),
    "Manh_San_Nha": ("Mạnh sân nhà", "home_win", 6, lambda v: v >= 55, lambda v: v < 40, "thắng sân nhà", True),
    "Yeu_San_Khach": ("Yếu sân khách", "away_loss", 6, lambda v: v >= 50, lambda v: v < 30, "thua sân khách", True),
    "Cay_Goc": ("Cày góc", "corners_for", 8, lambda v: v >= 5.5, lambda v: v < 4.5, "góc kiếm/trận", False),
    "Thuc_Dung_Pha_Loi_Choi": ("Thực dụng, phá lối chơi", "over25", 10, lambda v: v <= 45, lambda v: v > 60, "Tài 2.5", True),
    "Bat_Nat_Doi_Yeu_Chap_Sau": ("Bắt nạt đội yếu chấp sâu", None, 0, None, None, "", False),
    "Cong_Lam_Thu_Pha_BTTS": ("Công làm thủ phá (BTTS)", "btts", 10, lambda v: v >= 55, lambda v: v < 40, "hai đội cùng ghi bàn", True),
    "Xa_Goc_Chiu_Goc": ("Xả góc, chịu góc", "corners_against", 8, lambda v: v >= 5.5, lambda v: v < 4.5, "góc chịu/trận", False),
    "Danh_Phu_Dau_Tai_H1": ("Đánh phủ đầu Tài H1", "first_half_goal", 8, lambda v: v >= 72, lambda v: v < 60, "trận có bàn hiệp 1", True),
    "Bung_No_Cuoi_Tran_H2": ("Bùng nổ cuối trận H2", "second_half_goals", 8, lambda v: v >= 1.6, lambda v: v < 1.2, "bàn hiệp 2/trận", False),
    "Da_Rat_Cay_The": ("Đá rát, cày thẻ", "cards_for", 8, lambda v: v >= 2.4, lambda v: v < 1.8, "thẻ của đội/trận", False),
}

# Giống LeagueRules trong DnaVerifier.cs.
LEAGUE_RULES = {
    "Tai_Ban_Thang": ("Tài bàn thắng", "over25", 40, lambda v: v >= 55, lambda v: v < 48, "Tài 2.5", True),
    "Xiu_Ban_Thang": ("Xỉu bàn thắng", "over25", 40, lambda v: v <= 47, lambda v: v > 53, "Tài 2.5", True),
    "Thanh_Dia_San_Nha": ("Thánh địa sân nhà", "home_win", 40, lambda v: v >= 47, lambda v: v < 42, "chủ nhà thắng", True),
    "Tai_Goc": ("Tài góc", "corners_total", 30, lambda v: v >= 10.0, lambda v: v < 9.3, "góc/trận", False),
    "Xiu_Goc": ("Xỉu góc", "corners_total", 30, lambda v: v <= 9.3, lambda v: v > 10.0, "góc/trận", False),
    "Ban_Muon_Duoi_Suc_Cuoi_Tran": ("Bàn muộn, đuối sức cuối trận", "late_goal", 30, lambda v: v >= 40, lambda v: v < 33, "trận có bàn sau phút 75", True),
    "Cua_Duoi_Hay_An_Dong_Deu": ("Cửa dưới hay ăn, đồng đều", None, 0, None, None, "", False),
    "Phan_Hoa_Lon_Tin_Keo_Tren": ("Phân hoá lớn, tín kèo trên", None, 0, None, None, "", False),
    "Vua_Hoa_Thang_Sat_Nut": ("Vua hoà, thắng sát nút", "draw", 40, lambda v: v >= 27, lambda v: v < 22, "hoà", True),
    "No_Som_Hiep_1": ("Nổ sớm hiệp 1", "first_half_goal", 30, lambda v: v >= 72, lambda v: v < 65, "trận có bàn hiệp 1", True),
    "Mua_The_De_The_Do_Pen": ("Mưa thẻ, dễ thẻ đỏ / pen", "cards_total", 30, lambda v: v >= 4.8, lambda v: v < 4.2, "thẻ/trận", False),
}

# Cột số liệu → (chỉ số, số chữ số thập phân).
TEAM_STATS = {
    "TB_Tong_Ban": ("goals", 2),
    "TB_Ban_Ghi": ("goals_for", 2),
    "TB_Ban_Thua": ("goals_against", 2),
    "Ty_Le_Tai_2_5_Pct": ("over25", 1),
    "Ty_Le_BTTS_Pct": ("btts", 1),
    "Thang_San_Nha_Pct": ("home_win", 1),
    "Thua_San_Khach_Pct": ("away_loss", 1),
    "TB_Goc_Tu_Kiem": ("corners_for", 2),
    "TB_Goc_Phai_Chiu": ("corners_against", 2),
    "Ty_Le_Co_Ban_H1_Pct": ("first_half_goal", 1),
    "TB_Ban_H2": ("second_half_goals", 2),
    "TB_The_Phat": ("cards_for", 2),
}

LEAGUE_STATS = {
    "TB_Ban_Moi_Tran": ("goals", 2),
    "Ty_Le_Tai_2_5_Pct": ("over25", 1),
    "Ty_Le_Hoa_Pct": ("draw", 1),
    "Ty_Le_Chu_Nha_Thang_Pct": ("home_win", 1),
    "TB_Tong_Goc": ("corners_total", 1),
    "Ty_Le_Co_Ban_H1_Pct": ("first_half_goal", 1),
    "Ty_Le_Ban_Sau_Phut_75_Pct": ("late_goal", 1),
    "TB_The_Vang_Do": ("cards_total", 1),
}

# Ngưỡng coi số liệu file lệch API đáng kể (để ghi cảnh báo trong file đối chiếu).
STAT_TOLERANCE = {"over25": 10, "btts": 10, "home_win": 12, "away_loss": 12, "draw": 8, "first_half_goal": 10, "late_goal": 10,
                  "goals": 0.4, "goals_for": 0.35, "goals_against": 0.35, "corners_for": 1.0, "corners_against": 1.0,
                  "corners_total": 1.0, "second_half_goals": 0.35, "cards_for": 0.6, "cards_total": 0.8}


class ApiError(RuntimeError):
    """API trả lỗi trong trường "errors" (kể cả lỗi phía API-Football như mã 5xEr)."""


class Api:
    def __init__(self, key: str, cache: Path, delay: float):
        self.key, self.cache, self.delay = key, cache, delay
        self.requests = 0
        self.remaining: int | None = None

    def get(self, path: str) -> dict:
        if self.remaining is not None and self.remaining < MIN_REMAINING:
            sys.exit(f"Dừng: quota hôm nay chỉ còn {self.remaining} request (< {MIN_REMAINING}).")
        for attempt in (1, 2, 3):
            time.sleep(self.delay)
            req = urllib.request.Request(BASE + path, headers={"x-apisports-key": self.key})
            try:
                with urllib.request.urlopen(req, timeout=90) as r:
                    self.requests += 1
                    if (rem := r.headers.get("x-ratelimit-requests-remaining")) and rem.isdigit():
                        self.remaining = int(rem)
                    body = json.load(r)
            except urllib.error.HTTPError as e:
                self.requests += 1
                if e.code in (429, 500, 502, 503, 504) and attempt < 3:
                    time.sleep(30 if e.code == 429 else 5)
                    continue
                raise
            errors = body.get("errors")
            if errors and (isinstance(errors, dict) and errors or isinstance(errors, list) and len(errors) > 0):
                if "rateLimit" in json.dumps(errors) and attempt < 3:
                    time.sleep(30)
                    continue
                raise ApiError(f"API-Football lỗi {errors} ({path})")
            return body
        raise RuntimeError(f"Không gọi được {path}")

    def all_pages(self, path: str) -> list:
        items, page = [], 1
        while True:
            body = self.get(path if page == 1 else f"{path}&page={page}")
            items += body.get("response") or []
            paging = body.get("paging") or {}
            if paging.get("current", 1) >= paging.get("total", 1):
                return items
            page += 1

    def cached(self, name: str, path: str) -> list:
        file = self.cache / name
        if file.exists():
            return json.loads(file.read_text())
        items = self.all_pages(path)
        file.write_text(json.dumps(items))
        return items


def fetch_details(api: Api, ids: list[str], failed: list[str]) -> list:
    """fixtures?ids cho một nhóm trận; API lỗi thì lấy lại từng trận, trận vẫn lỗi thì bỏ qua (chạy lại sẽ thử lại)."""
    try:
        return api.get("fixtures?ids=" + "-".join(ids)).get("response") or []
    except ApiError:
        if len(ids) == 1:
            failed.append(ids[0])
            return []
    items = []
    for fixture_id in ids:
        items += fetch_details(api, [fixture_id], failed)
    return items


def parse_date(text: str) -> dt.date | None:
    try:
        return dt.date.fromisoformat(text[:10])
    except (TypeError, ValueError):
        return None


def seasons_in_window(league_items: list, start: dt.date, end: dt.date) -> list[tuple[int, dict]]:
    """Các mùa giao với khung [start, end], kèm độ phủ."""
    result = []
    for item in league_items[:1]:
        for s in item.get("seasons") or []:
            s_start, s_end = parse_date(s.get("start")), parse_date(s.get("end"))
            if s_start and s_end and s_start <= end and s_end >= start:
                result.append((s["year"], (s.get("coverage") or {}).get("fixtures") or {}))
    return result


def stat_int(stats: list, kind: str):
    for s in stats:
        if (s.get("type") or "").lower() == kind.lower():
            v = s.get("value")
            if isinstance(v, int):
                return v
            if isinstance(v, str) and v.rstrip("%").isdigit():
                return int(v.rstrip("%"))
            return 0  # có bảng thống kê mà giá trị null nghĩa là 0 (giống ApiMapper.ApplyStats)
    return 0


def compact(item: dict) -> dict:
    """Giữ phần cần dùng của một trận trong fixtures?ids (thống kê góc/thẻ, phút ghi bàn) để cache nhỏ."""
    home_id, away_id = item["teams"]["home"]["id"], item["teams"]["away"]["id"]
    tables = {t["team"]["id"]: t.get("statistics") or [] for t in item.get("statistics") or []}
    record = {"corners": None, "cards": None, "minutes": None}
    home, away = tables.get(home_id), tables.get(away_id)
    if home and away:
        record["corners"] = [stat_int(home, "Corner Kicks"), stat_int(away, "Corner Kicks")]
        record["cards"] = [stat_int(home, "Yellow Cards") + stat_int(home, "Red Cards"), stat_int(away, "Yellow Cards") + stat_int(away, "Red Cards")]
    goals = [e for e in item.get("events") or []
             if (e.get("type") or "").lower() == "goal" and (e.get("detail") or "").lower() != "missed penalty"
             and isinstance((e.get("time") or {}).get("elapsed"), int) and e["time"]["elapsed"] <= 120]
    if goals:
        # Phút X+Y tính là X+1 (giống DnaVerifier.LateGoal).
        record["minutes"] = [e["time"]["elapsed"] + (1 if (e["time"].get("extra") or 0) > 0 else 0) for e in goals]
    return record


def match_row(fx: dict, details: dict | None) -> dict:
    """Một trận đã đá: tỉ số 90 phút (giống ApiMapper.Apply), tỉ số hiệp 1, góc, thẻ, có bàn sau phút 75."""
    score = fx.get("score") or {}
    ft, ht = score.get("fulltime") or {}, score.get("halftime") or {}
    goals = fx.get("goals") or {}
    hg = ft.get("home") if ft.get("home") is not None else goals.get("home")
    ag = ft.get("away") if ft.get("away") is not None else goals.get("away")
    if hg is None or ag is None:
        return {}
    row = {
        "id": fx["fixture"]["id"], "home": fx["teams"]["home"]["id"], "away": fx["teams"]["away"]["id"],
        "home_name": fx["teams"]["home"]["name"], "away_name": fx["teams"]["away"]["name"],
        "hg": hg, "ag": ag, "hth": ht.get("home"), "hta": ht.get("away"),
        "corners": None, "cards": None, "late": None,
    }
    if details:
        row["corners"], row["cards"] = details.get("corners"), details.get("cards")
        if hg + ag == 0:
            row["late"] = False
        elif details.get("minutes"):
            row["late"] = any(m >= 76 for m in details["minutes"])
    elif hg + ag == 0:
        row["late"] = False
    return row


def rate(values) -> tuple[float | None, int]:
    vals = [v for v in values if v is not None]
    return (100.0 * sum(1 for v in vals if v) / len(vals), len(vals)) if vals else (None, 0)


def avg(values) -> tuple[float | None, int]:
    vals = [v for v in values if v is not None]
    return (sum(vals) / len(vals), len(vals)) if vals else (None, 0)


def stats(records: list[dict]) -> dict:
    """Records nhìn từ một phía: gf, ga, is_home, htf, hta, cf, ca, kf, ka, late (giống DnaVerifier.Stats)."""
    def ht_total(r):
        return None if r["htf"] is None or r["hta"] is None else r["htf"] + r["hta"]

    return {
        "matches": len(records),
        "home_matches": sum(1 for r in records if r["is_home"]),
        "away_matches": sum(1 for r in records if not r["is_home"]),
        "goals": avg(r["gf"] + r["ga"] for r in records),
        "goals_for": avg(r["gf"] for r in records),
        "goals_against": avg(r["ga"] for r in records),
        "over25": rate(r["gf"] + r["ga"] > 2 for r in records),
        "btts": rate(r["gf"] > 0 and r["ga"] > 0 for r in records),
        "draw": rate(r["gf"] == r["ga"] for r in records),
        "home_win": rate(r["gf"] > r["ga"] for r in records if r["is_home"]),
        "away_loss": rate(r["gf"] < r["ga"] for r in records if not r["is_home"]),
        "corners_for": avg(r["cf"] if r["ca"] is not None else None for r in records),
        "corners_against": avg(r["ca"] if r["cf"] is not None else None for r in records),
        "corners_total": avg(r["cf"] + r["ca"] if r["cf"] is not None and r["ca"] is not None else None for r in records),
        "first_half_goal": rate(None if ht_total(r) is None else ht_total(r) > 0 for r in records),
        "second_half_goals": avg(None if ht_total(r) is None else r["gf"] + r["ga"] - ht_total(r) for r in records),
        "late_goal": rate(r["late"] for r in records),
        "cards_for": avg(r["kf"] if r["ka"] is not None else None for r in records),
        "cards_total": avg(r["kf"] + r["ka"] if r["kf"] is not None and r["ka"] is not None else None for r in records),
    }


def side(row: dict, home: bool) -> dict:
    c, k = row["corners"], row["cards"]
    i, j = (0, 1) if home else (1, 0)
    return {
        "is_home": home,
        "gf": row["hg"] if home else row["ag"], "ga": row["ag"] if home else row["hg"],
        "htf": row["hth"] if home else row["hta"], "hta": row["hta"] if home else row["hth"],
        "cf": c[i] if c else None, "ca": c[j] if c else None,
        "kf": k[i] if k else None, "ka": k[j] if k else None,
        "late": row["late"],
    }


def num(value: float | None, places: int) -> str:
    if value is None:
        return ""
    text = f"{round(value, places):.{places}f}"
    return text.rstrip("0").rstrip(".") if "." in text else text


def check(rules: dict, st: dict, col: str) -> tuple[str, str]:
    """Trạng thái một nhãn: khop / luong_chung / nguoc / thieu / kem_keo, kèm mô tả số liệu."""
    label, metric, min_sample, good, bad, name, percent = rules[col]
    if metric is None:
        return "kem_keo", "cần dữ liệu kèo, không kiểm được bằng tỉ số"
    value, sample = st[metric]
    if value is None or sample < min_sample:
        return "thieu", f"chưa đủ số liệu ({sample} trận có số liệu, cần ≥ {min_sample})"
    shown = f"{value:.0f}%" if percent else f"{value:.2f}".rstrip("0").rstrip(".")
    text = f"{name} = {shown} ({sample} trận)"
    return ("nguoc" if bad(value) else "khop" if good(value) else "luong_chung"), text


def flag_of(file_value: str) -> int:
    return 1 if (file_value or "").strip() == "1" else 0


def decide(rules: dict, st: dict, file_row: dict) -> dict[str, tuple[int, str, str]]:
    """Nhãn theo API cho từng cột: (giá trị, trạng thái, mô tả)."""
    result = {}
    for col in rules:
        status, text = check(rules, st, col)
        file_flag = flag_of(file_row.get(col, ""))
        value = 1 if status == "khop" else 0 if status in ("nguoc", "luong_chung") else file_flag
        result[col] = (value, status, text)
    return result


def tag_cell(file_flag: int, api_flag: int, status: str, text: str) -> str:
    if status in ("thieu", "kem_keo"):
        return f"giữ {file_flag} ({text})"
    if file_flag == api_flag:
        return f"đúng {file_flag} ({text})"
    if file_flag == 1:
        return f"1→0 {'SAI' if status == 'nguoc' else 'bỏ, lưng chừng'} ({text})"
    return f"0→1 thiếu nhãn ({text})"


def reasons(rules: dict, decided: dict) -> str:
    parts = []
    for col, (value, status, text) in decided.items():
        label = rules[col][0]
        if status in ("thieu", "kem_keo"):
            if value == 1:
                parts.append(f"[{label}] giữ theo file: {text}")
        elif value == 1:
            parts.append(f"[{label}] {text}")
    return " | ".join(parts) or "Không nhãn nào đạt ngưỡng."


def stat_gaps(stat_cols: dict, st: dict, file_row: dict) -> list[str]:
    gaps = []
    for col, (metric, places) in stat_cols.items():
        api_value = st[metric][0]
        try:
            file_value = float(file_row.get(col, "") or "nan")
        except ValueError:
            continue
        if api_value is None or file_value != file_value:
            continue
        if abs(api_value - file_value) > STAT_TOLERANCE[metric]:
            gaps.append(f"{col} file {num(file_value, places)} / API {num(api_value, places)}")
    return gaps


def write_csv(path: Path, header: list[str], rows: list[dict]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, fieldnames=header, quoting=csv.QUOTE_ALL, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", type=Path, default=HERE / "tu_api", help="thư mục ghi 4 file CSV")
    parser.add_argument("--cache", type=Path, default=HERE / ".cache", help="thư mục lưu phản hồi API")
    parser.add_argument("--end", type=dt.date.fromisoformat, default=dt.datetime.now(dt.timezone.utc).date(), help="ngày cuối khung 1 năm (mặc định hôm nay, UTC)")
    parser.add_argument("--delay", type=float, default=0.2, help="giây nghỉ giữa hai request")
    args = parser.parse_args()

    key = os.environ.get("APIFOOTBALL_KEY", "").strip()
    if not key:
        sys.exit("Thiếu APIFOOTBALL_KEY (key API-Football).")
    end = args.end
    start = end - dt.timedelta(days=364)
    args.out.mkdir(parents=True, exist_ok=True)
    args.cache.mkdir(parents=True, exist_ok=True)
    api = Api(key, args.cache, args.delay)

    team_rows = list(csv.DictReader(TEAM_FILE.open(encoding="utf-8-sig")))
    team_header = list(team_rows[0].keys())
    league_rows = list(csv.DictReader(LEAGUE_FILE.open(encoding="utf-8-sig")))
    league_header = list(league_rows[0].keys())
    ids = {(r["League_Id"], r["Doi_Bong"]): r for r in csv.DictReader(IDS_FILE.open(encoding="utf-8"))}
    league_ids = sorted({int(r["League_Id"]) for r in team_rows} | {int(r["League_Id"]) for r in league_rows})

    details_file = args.cache / "fixture_details.json"
    details: dict[str, dict] = json.loads(details_file.read_text()) if details_file.exists() else {}
    tag = end.isoformat()
    matches: dict[int, list[dict]] = {}
    no_stats: set[int] = set()
    names: dict[int, str] = {}
    league_names: dict[int, str] = {}
    failed: list[str] = []
    for n, league_id in enumerate(league_ids, 1):
        seasons = seasons_in_window(api.cached(f"league_{league_id}_{tag}.json", f"leagues?id={league_id}"), start, end)
        rows = []
        for season, coverage in seasons:
            fixtures = api.cached(f"fixtures_{league_id}_{season}_{tag}.json", f"fixtures?league={league_id}&season={season}")
            done = [f for f in fixtures if (f["fixture"]["status"]["short"] in FINISHED)
                    and (d := parse_date(f["fixture"]["date"])) and start <= d <= end]
            if coverage.get("statistics_fixtures") or coverage.get("events"):
                missing = [str(f["fixture"]["id"]) for f in done if str(f["fixture"]["id"]) not in details]
                for i in range(0, len(missing), 20):
                    for item in fetch_details(api, missing[i:i + 20], failed):
                        details[str(item["fixture"]["id"])] = compact(item)
                    details_file.write_text(json.dumps(details))
            else:
                no_stats.add(league_id)
            for f in done:
                league_names.setdefault(league_id, f["league"].get("name") or str(league_id))
                row = match_row(f, details.get(str(f["fixture"]["id"])))
                if row:
                    rows.append(row)
                    names[row["home"]], names[row["away"]] = row["home_name"], row["away_name"]
        matches[league_id] = list({r["id"]: r for r in rows}.values())
        print(f"[{n}/{len(league_ids)}] giải {league_id}: {len(matches[league_id])} trận, mùa {[s for s, _ in seasons]} · đã gọi {api.requests} request", flush=True)

    window = f"{start:%d/%m/%Y}–{end:%d/%m/%Y}"
    # Đội đá ở những giải nào (trong 46 giải) — để chỉ ra giải đúng khi file ghi sai giải.
    played: dict[int, dict[int, int]] = defaultdict(lambda: defaultdict(int))
    for league_id, rows in matches.items():
        for m in rows:
            played[m["home"]][league_id] += 1
            played[m["away"]][league_id] += 1

    # ---- Đội
    def team_of(r: dict) -> int | None:
        id_row = ids.get((r["League_Id"], r["Doi_Bong"])) or {}
        return int(id_row["TeamId"]) if id_row.get("TeamId") else None

    # Dòng file có trận thật ở đúng giải (để quyết định dòng sai giải: bỏ, chuyển giải hay xoá đội).
    valid_rows: dict[int, int] = defaultdict(int)
    for r in team_rows:
        if (tid := team_of(r)) is not None and played.get(tid, {}).get(int(r["League_Id"]), 0) > 0:
            valid_rows[tid] += 1
    file_league_names = {int(r["League_Id"]): r["Giai_Dau"] for r in league_rows} | {int(r["League_Id"]): r["Giai_Dau"] for r in team_rows}

    api_rows, compare_rows, api_ids, deleted = [], [], [], []
    relocated: set[int] = set()
    for src in team_rows:
        r = src
        league_id = int(r["League_Id"])
        id_row = ids.get((r["League_Id"], r["Doi_Bong"])) or {}
        team_id = team_of(r)
        action = ""
        warnings = []
        elsewhere = sorted(played.get(team_id, {}).items(), key=lambda x: -x[1]) if team_id is not None else []
        if team_id is not None and played.get(team_id, {}).get(league_id, 0) == 0:
            warnings.append("0 trận ở giải này trong 1 năm: " + (
                "đội đá ở " + ", ".join(f"{league_names.get(l, l)} (id {l}, {c} trận)" for l, c in elsewhere)
                if elsewhere else "không đá giải nào trong 46 giải (xuống hạng thấp hơn, giải thể hoặc sai đội)"))
            if valid_rows[team_id] > 0 or team_id in relocated:
                action = "bỏ dòng (đội đã có dòng đúng ở giải khác)"
            elif elsewhere:
                target = elsewhere[0][0]
                action = f"chuyển sang {file_league_names.get(target) or league_names.get(target, target)} (id {target})"
                relocated.add(team_id)
                r = dict(r, League_Id=str(target), Giai_Dau=file_league_names.get(target) or league_names.get(target, str(target)))
                league_id = target
            else:
                action = "xoá đội (không đá giải nào trong 46 giải)"
                deleted.append({"League_Id": r["League_Id"], "Giai_Dau": r["Giai_Dau"], "Doi_Bong": r["Doi_Bong"], "TeamId": team_id,
                                "Ten_API": id_row.get("Api_Name", ""), "Ly_Do": warnings[-1]})
        records = [side(m, m["home"] == team_id) for m in matches.get(league_id, []) if team_id in (m["home"], m["away"])]
        st = stats(records)
        decided = decide(TEAM_RULES, st, r)
        if team_id is None:
            warnings.append("chưa có id đội trong team-ids.csv")
        method = id_row.get("Match", "")
        api_name = names.get(team_id) or id_row.get("Api_Name", "")
        file_matches = int(r["So_Tran_1_Nam"] or 0)
        count_gap = st["matches"] and abs(file_matches - st["matches"]) > max(5, 0.3 * st["matches"])
        # Khớp tên kiểu contains/alias phần lớn đúng (Leeds United ↔ Leeds): chỉ nhắc khi khớp mờ hoặc có dấu hiệu khác.
        if method and (method.startswith(("fuzzy", "other")) or not method.startswith(("exact", "normalized")) and (st["matches"] == 0 or count_gap)):
            warnings.append(f"tên khớp gần đúng ({method}): file '{r['Doi_Bong']}' ↔ API '{api_name}'")
        if count_gap:
            warnings.append(f"số trận lệch: file {file_matches}, API {st['matches']}")
        if league_id in no_stats or (st["matches"] and st["corners_for"][1] == 0):
            warnings.append("API không có số liệu góc/thẻ giải này: giữ nhãn góc/thẻ theo file")
        gaps = stat_gaps(TEAM_STATS, st, r) if st["matches"] else []

        out = dict(r)
        out["So_Tran_1_Nam"] = str(st["matches"])
        for col, (metric, places) in TEAM_STATS.items():
            out[col] = num(st[metric][0], places)
        for col, (value, _, _) in decided.items():
            out[col] = str(value)
        out["Ghi_Chu"] = f"Tính từ API-Football {window}: {st['matches']} trận ({st['home_matches']} sân nhà, {st['away_matches']} sân khách)." \
            + (" Cảnh báo: " + "; ".join(warnings) if warnings else "")
        out["Phan_Tich_Ly_Do_Chon_1_Nam"] = reasons(TEAM_RULES, decided)
        if action.startswith("chuyển"):
            out["Ghi_Chu"] += f" Chuyển từ {src['Giai_Dau']} (file ghi sai giải)."
        if not action.startswith(("bỏ", "xoá")):
            api_rows.append(out)
            api_ids.append({"League_Id": out["League_Id"], "Doi_Bong": out["Doi_Bong"], "TeamId": team_id or "",
                            "Api_Name": names.get(team_id) or id_row.get("Api_Name", ""), "Match": id_row.get("Match", "")})

        changed = [c for c, (v, s, _) in decided.items() if v != flag_of(r[c])]
        wrong = [c for c, (v, s, _) in decided.items() if flag_of(r[c]) == 1 and s == "nguoc"]
        cmp = {
            "League_Id": src["League_Id"], "Giai_Dau": src["Giai_Dau"], "Doi_Bong_File": r["Doi_Bong"],
            "TeamId": team_id or "", "Ten_API": api_name, "Cach_Khop_Ten": method,
            "So_Tran_File": file_matches, "So_Tran_API": st["matches"],
            "So_Nhan_Doi": len(changed), "So_Nhan_Sai_Ro": len(wrong),
            "Nhan_Doi": "; ".join(f"{TEAM_RULES[c][0]} {flag_of(r[c])}→{decided[c][0]}" for c in changed),
            "Canh_Bao": "; ".join(warnings), "So_Lieu_Lech": "; ".join(gaps), "Xu_Ly": action,
        }
        for col, (value, status, text) in decided.items():
            cmp[col] = tag_cell(flag_of(r[col]), value, status, text)
        compare_rows.append(cmp)

    tag_cols = list(TEAM_RULES)
    compare_header = ["League_Id", "Giai_Dau", "Doi_Bong_File", "TeamId", "Ten_API", "Cach_Khop_Ten", "So_Tran_File", "So_Tran_API",
                      "Xu_Ly", "So_Nhan_Doi", "So_Nhan_Sai_Ro", "Nhan_Doi", "Canh_Bao", "So_Lieu_Lech"] + tag_cols
    compare_rows.sort(key=lambda c: (-(c["So_Tran_API"] == 0), -c["So_Nhan_Sai_Ro"], -c["So_Nhan_Doi"], -bool(c["Canh_Bao"]), c["Giai_Dau"], c["Doi_Bong_File"]))
    write_csv(args.out / "dna_doi_bong_30_giai_1nam_API.csv", team_header, api_rows)
    write_csv(args.out / "dna_doi_bong_doi_chieu.csv", compare_header, compare_rows)
    # Id đội theo đúng dòng của file API (dòng chuyển giải có League_Id mới) — dùng với gen_team_dna_sql.py --ids.
    write_csv(args.out / "team-ids_API.csv", ["League_Id", "Doi_Bong", "TeamId", "Api_Name", "Match"], api_ids)
    write_csv(args.out / "dna_doi_bong_xoa.csv", ["League_Id", "Giai_Dau", "Doi_Bong", "TeamId", "Ten_API", "Ly_Do"], deleted)

    # ---- Giải (mỗi trận một lần, nhìn từ đội nhà)
    league_api, league_cmp = [], []
    for r in league_rows:
        league_id = int(r["League_Id"])
        st = stats([side(m, True) for m in matches.get(league_id, [])])
        decided = decide(LEAGUE_RULES, st, r)
        warnings = []
        if st["matches"] < 40:
            warnings.append(f"chỉ {st['matches']} trận trong 1 năm")
        if league_id in no_stats or (st["matches"] and st["corners_total"][1] == 0):
            warnings.append("API không có số liệu góc/thẻ: giữ nhãn góc/thẻ theo file")
        gaps = stat_gaps(LEAGUE_STATS, st, r) if st["matches"] else []
        out = dict(r)
        for col, (metric, places) in LEAGUE_STATS.items():
            out[col] = num(st[metric][0], places)
        for col, (value, _, _) in decided.items():
            out[col] = str(value)
        out["Ghi_Chu"] = f"Tính từ API-Football {window}: {st['matches']} trận." + (" Cảnh báo: " + "; ".join(warnings) if warnings else "")
        out["Phan_Tich_Ly_Do_Chon_1_Nam"] = reasons(LEAGUE_RULES, decided)
        league_api.append(out)

        changed = [c for c, (v, s, _) in decided.items() if v != flag_of(r[c])]
        wrong = [c for c, (v, s, _) in decided.items() if flag_of(r[c]) == 1 and s == "nguoc"]
        cmp = {
            "League_Id": r["League_Id"], "Giai_Dau": r["Giai_Dau"], "So_Tran_API": st["matches"],
            "So_Nhan_Doi": len(changed), "So_Nhan_Sai_Ro": len(wrong),
            "Nhan_Doi": "; ".join(f"{LEAGUE_RULES[c][0]} {flag_of(r[c])}→{decided[c][0]}" for c in changed),
            "Canh_Bao": "; ".join(warnings), "So_Lieu_Lech": "; ".join(gaps),
        }
        for col, (value, status, text) in decided.items():
            cmp[col] = tag_cell(flag_of(r[col]), value, status, text)
        league_cmp.append(cmp)

    league_cmp.sort(key=lambda c: (-c["So_Nhan_Sai_Ro"], -c["So_Nhan_Doi"], c["Giai_Dau"]))
    write_csv(args.out / "dna_giai_dau_46_giai_1nam_API.csv", league_header, league_api)
    write_csv(args.out / "dna_giai_dau_doi_chieu.csv",
              ["League_Id", "Giai_Dau", "So_Tran_API", "So_Nhan_Doi", "So_Nhan_Sai_Ro", "Nhan_Doi", "Canh_Bao", "So_Lieu_Lech"] + list(LEAGUE_RULES),
              league_cmp)

    teams_changed = sum(1 for c in compare_rows if c["So_Nhan_Doi"])
    teams_wrong = sum(1 for c in compare_rows if c["So_Nhan_Sai_Ro"])
    zero = sum(1 for c in compare_rows if c["So_Tran_API"] == 0)
    print(f"Khung {window} · {api.requests} request (còn {api.remaining} hôm nay)")
    if failed:
        print(f"{len(failed)} trận API báo lỗi khi lấy thống kê (bỏ góc/thẻ/phút ghi bàn của các trận này, tỉ số vẫn tính): {', '.join(failed)}")
    print(f"Đội: {len(compare_rows)} dòng · {teams_changed} dòng đổi nhãn · {teams_wrong} dòng có nhãn sai rõ · {zero} dòng 0 trận "
          f"(bỏ {sum(1 for c in compare_rows if c['Xu_Ly'].startswith('bỏ'))}, chuyển giải {len(relocated)}, xoá đội {len(deleted)})")
    print(f"Giải: {len(league_cmp)} giải · {sum(1 for c in league_cmp if c['So_Nhan_Doi'])} giải đổi nhãn")
    print(f"Đã ghi 4 file vào {args.out}")


if __name__ == "__main__":
    main()
