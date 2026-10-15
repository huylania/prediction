"""Tìm id API-Football cho các đội trong dna_doi_bong_22_giai_1nam.csv (CSV chỉ có tên).

Gọi teams?league=<id>&season=<năm> cho mỗi giải (mùa 2024–2026), đối chiếu tên trong cùng giải
rồi ghi team-ids.csv. Đội không khớp được ghi TeamId trống để sửa tay.

    APIFOOTBALL_KEY=... python3 scripts/dna/resolve_team_ids.py [--cache thư_mục]
"""
import argparse
import csv
import difflib
import json
import os
import re
import sys
import time
import unicodedata
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
SOURCE = HERE / "dna_doi_bong_22_giai_1nam.csv"
OUTPUT = HERE / "team-ids.csv"
SEASONS = (2026, 2025, 2024)

# Tên trong CSV khác hẳn tên API (đổi tên, viết tắt): ghi tay ở đây "tên CSV": "tên API".
ALIASES: dict[str, str] = {
    "Al Ahli": "Al-Ahli Jeddah",  # C1 châu Á: Al-Ahli Saudi (không phải Shabab Al Ahli Dubai) — nên kiểm tra
    "Al Wehdat": "Al Wihdat",
    "Athletico-PR": "Atletico Paranaense",
    "Athletico Paranaense": "Atletico Paranaense",  # tránh nhầm "Athletic Club" (MG) cùng giải
    "Cong An Ha Noi": "Công An Nhân Dân",  # không phải Ha Noi FC
    "Shanghai Port": "SHANGHAI SIPG",  # không phải Port FC (Thái)
    "Celta B": "Celta de Vigo II",
    "Chengdu Rongcheng": "Chengdu Better City",  # tên cũ trên API
    "Cong An Hanoi": "Công An Nhân Dân",  # tên cũ trên API
    "F.C. København": "FC Copenhagen",
    "Inter Milan": "Inter",
    "LA Galaxy": "Los Angeles Galaxy",
    "Lion City Sailors FC": "Home United",  # tên cũ trên API
    "Mahasarakham SBT": "Mahasarakham United",
    "Nakhon Si United": "Nakhon Si Thammarat",
    "Olympiacos": "Olympiakos Piraeus",
    "Slavia Prague": "Slavia Praha",
    "Sociedad B": "Real Sociedad II",
    "Sp Gijon": "Sporting Gijon",
    "Stade Rennais": "Rennes",
    "Ulsan HD": "Ulsan Hyundai FC",
    "Wolverhampton Wanderers": "Wolves",
    "Club Atlético Morelia": "Monarcas",  # tên cũ trên API
    "Club Jaiba Brava": "CDS Tampico Madero",
    "Madura United FC": "Persepam Madura Utd",
    "Malut United": "Java United",  # API không có "Malut United"; "Java United" (id 22409) đứng đúng chỗ ở Liga 1 2024–2025 — nên kiểm tra
}

NOISE = {"fc", "cf", "sc", "ac", "afc", "fk", "sk", "cd", "ud", "sd", "rc", "rcd", "club", "de", "the", "cr", "ec", "se", "ca", "sv", "vfb", "vfl", "tsg", "us", "ss", "as"}


def norm(name: str) -> str:
    text = unicodedata.normalize("NFKD", name).encode("ascii", "ignore").decode().lower()
    text = re.sub(r"[^a-z0-9 ]+", " ", text.replace("&", " and "))
    return " ".join(w for w in text.split() if w not in NOISE) or text.strip()


def fetch(league: int, season: int, key: str, cache: Path) -> list[dict]:
    path = cache / f"teams_{league}_{season}.json"
    if path.exists():
        data = json.loads(path.read_text())
    else:
        req = urllib.request.Request(f"https://v3.football.api-sports.io/teams?league={league}&season={season}",
                                     headers={"x-apisports-key": key})
        with urllib.request.urlopen(req, timeout=30) as resp:
            data = json.loads(resp.read())
        path.write_text(json.dumps(data))
        time.sleep(0.4)
    if data.get("errors"):
        print(f"  ! giải {league} mùa {season}: {data['errors']}", file=sys.stderr)
    return [{"id": r["team"]["id"], "name": r["team"]["name"]} for r in data.get("response", [])]


def match(name: str, teams: list[dict]) -> tuple[dict | None, str]:
    target = ALIASES.get(name, name)
    for t in teams:
        if t["name"] == target:
            return t, "alias" if target != name else "exact"
    n = norm(target)
    same = [t for t in teams if norm(t["name"]) == n]
    if len(same) == 1:
        return same[0], "normalized"
    contains = [t for t in teams if n and (n in norm(t["name"]) or norm(t["name"]) in n)]
    if len(contains) == 1:
        return contains[0], "contains"
    scored = sorted(((difflib.SequenceMatcher(None, n, norm(t["name"])).ratio(), t) for t in teams), key=lambda x: -x[0])
    if scored and scored[0][0] >= 0.82 and (len(scored) == 1 or scored[0][0] - scored[1][0] >= 0.08):
        return scored[0][1], f"fuzzy {scored[0][0]:.2f}"
    return None, "not found"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--cache", default=str(HERE / ".cache"))
    args = parser.parse_args()
    key = os.environ.get("APIFOOTBALL_KEY", "")
    cache = Path(args.cache)
    cache.mkdir(parents=True, exist_ok=True)

    rows = list(csv.DictReader(SOURCE.open(encoding="utf-8-sig")))
    leagues = sorted({int(r["League_Id"]) for r in rows})
    teams_by_league: dict[int, list[dict]] = {}
    for league in leagues:
        seen: dict[int, dict] = {}
        for season in SEASONS:
            if not key and not (cache / f"teams_{league}_{season}.json").exists():
                sys.exit("Thiếu APIFOOTBALL_KEY")
            for t in fetch(league, season, key, cache):
                seen.setdefault(t["id"], t)
        teams_by_league[league] = list(seen.values())
        print(f"giải {league}: {len(seen)} đội trên API")

    out, missing = [], []
    for r in rows:
        league = int(r["League_Id"])
        team, how = match(r["Doi_Bong"], teams_by_league[league])
        out.append({"League_Id": league, "Doi_Bong": r["Doi_Bong"], "TeamId": team["id"] if team else "",
                    "Api_Name": team["name"] if team else "", "Match": how})
        if team is None:
            missing.append(f"{league} | {r['Doi_Bong']}")

    # Đội đã khớp ở giải khác (vd lên/xuống hạng) thì dùng lại id đó.
    known = {o["Doi_Bong"]: o for o in out if o["TeamId"] != ""}
    for o in out:
        if o["TeamId"] == "" and o["Doi_Bong"] in known:
            o["TeamId"], o["Api_Name"], o["Match"] = known[o["Doi_Bong"]]["TeamId"], known[o["Doi_Bong"]]["Api_Name"], "other league"
    missing = [f"{o['League_Id']} | {o['Doi_Bong']}" for o in out if o["TeamId"] == ""]

    with OUTPUT.open("w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=["League_Id", "Doi_Bong", "TeamId", "Api_Name", "Match"])
        w.writeheader()
        w.writerows(out)
    print(f"Đã ghi {OUTPUT.name}: {len(out) - len(missing)}/{len(out)} dòng có id.")
    for m in missing:
        print("  chưa khớp:", m)


if __name__ == "__main__":
    main()
