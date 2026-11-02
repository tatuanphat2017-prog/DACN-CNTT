"""Xuất dữ liệu huấn luyện từ backend ra CSV (dùng làm bộ dữ liệu nền đóng gói trong ML service).

Chạy (backend đang chạy, có file .env ở thư mục gốc repo):
    python scripts/export_training_data.py                       # lấy từ http://localhost:8080/api
    python scripts/export_training_data.py --api http://localhost:5199/api --out data/base_training.csv
"""
import argparse
import csv
import json
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
COLUMNS = ["absent_rate", "late_rate", "assignment_score", "midterm_score", "prior_gpa", "failed"]


def call(base: str, path: str, body: dict | None = None, token: str | None = None):
    request = urllib.request.Request(base + path, data=json.dumps(body).encode() if body else None, method="POST" if body else "GET")
    request.add_header("Content-Type", "application/json")
    if token:
        request.add_header("Authorization", "Bearer " + token)
    with urllib.request.urlopen(request, timeout=120) as response:
        return json.loads(response.read())


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--api", default="http://localhost:8080/api")
    parser.add_argument("--out", default=str(Path(__file__).resolve().parents[1] / "data" / "base_training.csv"))
    args = parser.parse_args()

    env = dict(line.rstrip("\n").split("=", 1) for line in open(ROOT / ".env", encoding="utf-8") if "=" in line and not line.startswith("#"))
    token = call(args.api, "/auth/login", {"username": "admin", "password": env["ADMIN_PASSWORD"]})["accessToken"]
    rows = call(args.api, "/ml/training-data", token=token)

    with open(args.out, "w", newline="", encoding="utf-8") as file:
        writer = csv.writer(file)
        writer.writerow(COLUMNS)
        for r in rows:
            writer.writerow([r["absentRate"], r["lateRate"], r["assignmentScore"] if r["assignmentScore"] is not None else "",
                             r["midtermScore"] if r["midtermScore"] is not None else "",
                             r["priorGpa"] if r["priorGpa"] is not None else "", int(r["failed"])])
    failed = sum(r["failed"] for r in rows)
    print(f"Đã ghi {len(rows)} dòng ({failed} rớt, {failed / len(rows):.1%}) vào {args.out}")


if __name__ == "__main__":
    main()
