"""Kiểm tra dữ liệu demo do DemoDataSeeder tạo (chạy trên database MỚI, chưa có dữ liệu cũ).

Chạy: python tests/api/test_demo_data.py
Nếu muốn thử trên database riêng: đặt API_BASE trỏ tới backend đó.
"""
import sys
from collections import Counter

from apitest_lib import *

admin = login("admin", ENV["ADMIN_PASSWORD"])
gv1 = login("GV001", ENV["DEMO_PASSWORD"])
sv1 = login("SV001", ENV["DEMO_PASSWORD"])
sv90 = login("SV090", ENV["DEMO_PASSWORD"])      # khóa 2025: sinh viên mới
assert admin and gv1 and sv1 and sv90, "Không đăng nhập được tài khoản demo"

print("== Quy mô dữ liệu ==")
import re


def all_users(role):
    out, page = [], 1
    while True:
        body = call("GET", f"/users?role={role}&pageSize=100&page={page}", token=admin)[1]
        out += body["items"]
        if len(out) >= body["totalItems"]:
            return out
        page += 1


# Chỉ đếm đúng tài khoản demo (database có thể còn tài khoản thử từ các test khác)
demo_students = [u for u in all_users("Student") if re.fullmatch(r"SV\d{3}", u["username"]) and u["isActive"]]
demo_lecturers = [u for u in all_users("Lecturer") if re.fullmatch(r"GV\d{3}", u["username"]) and u["isActive"]]
check("120 sinh viên demo SV001–SV120", [f"SV{i:03d}" for i in range(1, 121)], sorted(u["username"] for u in demo_students))
check("6 giảng viên demo GV001–GV006", [f"GV{i:03d}" for i in range(1, 7)], sorted(u["username"] for u in demo_lecturers))
check("Có tài khoản admin", True, any(u["username"] == "admin" for u in call("GET", "/users?role=Admin", token=admin)[1]["items"]))
check("16 môn học", 16, call("GET", "/courses?pageSize=100", token=admin)[1]["totalItems"])
sems = call("GET", "/semesters", token=admin)[1]
check("3 học kỳ", 3, len(sems))
cur = call("GET", "/semesters/current", token=admin)[1]
check("Học kỳ hiện tại = HK1 2026-2027", "HK1 2026-2027", cur["name"])
all_sections = call("GET", "/sections?pageSize=100", token=admin)[1]
check("48 lớp học phần", 48, all_sections["totalItems"])
by_sem = Counter(s["semesterName"] for s in all_sections["items"])
check("Mỗi học kỳ 16 lớp", {16}, set(by_sem.values()))
sizes = [s["studentCount"] for s in all_sections["items"]]
check("Sĩ số mỗi lớp từ 20 đến 60 sinh viên", True, all(20 <= n <= 60 for n in sizes))
check("Tổng đăng ký = 1800 (120 SV × 5 môn × 3 kỳ)", 1800, sum(sizes))

print("== Phân quyền theo giảng viên ==")
mine = call("GET", "/sections?pageSize=100", token=gv1)[1]["items"]
check("GV001 chỉ thấy lớp của mình (có lớp)", True, 0 < len(mine) < 48)
check("Tất cả lớp của GV001 do GV001 dạy", {"TS. Nguyễn Thị Lan"}, {s["lecturerName"] for s in mine})

print("== Lớp của học kỳ đang học (giữa kỳ) ==")
current = next(s for s in mine if s["semesterName"] == "HK1 2026-2027")
sid = current["id"]
rows = call("GET", f"/sections/{sid}/grades", token=gv1)[1]["rows"]
check("Có sinh viên trong lớp", True, len(rows) > 20)
check("Chưa có điểm cuối kỳ", {None}, {r["finalScore"] for r in rows})
check("Chưa có điểm tổng", {None}, {r["totalScore"] for r in rows})
check("Chưa có kết quả qua/rớt", {None}, {r["isPassed"] for r in rows})
check("Mọi sinh viên có điểm chuyên cần", True, all(r["attendanceScore"] is not None for r in rows))
cur_rows = [x for s in call("GET", f"/sections?semesterId={cur['id']}&pageSize=100", token=admin)[1]["items"]
            for x in call("GET", f"/sections/{s['id']}/grades", token=admin)[1]["rows"]]
no_mid = sum(r["midtermScore"] is None for r in cur_rows) / len(cur_rows)
no_asg = sum(r["assignmentScore"] is None for r in cur_rows) / len(cur_rows)
print(f"       Cả học kỳ hiện tại: {len(cur_rows)} đăng ký, thiếu giữa kỳ {no_mid:.1%}, thiếu bài tập {no_asg:.1%}")
check("Có dữ liệu thiếu thật nhưng ít (giữa kỳ thiếu < 10%)", True, 0 < no_mid < 0.10)
check("Có dữ liệu thiếu thật nhưng ít (bài tập thiếu < 15%)", True, 0 < no_asg < 0.15)
check("Mọi sinh viên có GPA tích lũy (đã học 2 kỳ trước)", True, all(r["priorGpa"] is not None for r in rows))
check("GPA trong khoảng 0–4", True, all(0 <= r["priorGpa"] <= 4 for r in rows))
sessions = call("GET", f"/sections/{sid}/attendance", token=gv1)[1]
held = [x for x in sessions if x["date"] is not None]
check("Đủ 15 buổi, đã học 8 buổi", (15, 8), (len(sessions), len(held)))
check("Ngày điểm danh không ở tương lai", True, all(x["date"] <= "2026-11-02" for x in held))

print("== Lớp của học kỳ đã kết thúc (có kết quả cuối kỳ) ==")
past_mine = next(s for s in mine if s["semesterName"] == "HK1 2025-2026")
prow = call("GET", f"/sections/{past_mine['id']}/grades", token=gv1)[1]["rows"]
check("Đủ điểm cuối kỳ", True, all(r["finalScore"] is not None for r in prow))
check("Đủ điểm tổng và qua/rớt", True, all(r["totalScore"] is not None and r["isPassed"] is not None for r in prow))
check("Kỳ đầu: sinh viên khóa 2025 chưa có GPA, khóa cũ có", True,
      all((r["priorGpa"] is None) == (int(r["studentCode"][2:]) > 80) for r in prow))
check("Điểm chuyên cần lớp đã kết thúc (15 buổi) trong 0–10", True, all(0 <= r["attendanceScore"] <= 10 for r in prow))

print("== Thống kê qua/rớt toàn bộ học kỳ đã kết thúc ==")
results = []
for s in all_sections["items"]:
    if s["semesterName"] == cur["name"]:
        continue
    for r in call("GET", f"/sections/{s['id']}/grades", token=admin)[1]["rows"]:
        results.append(r)
fail = sum(1 for r in results if r["isPassed"] is False)
rate = fail / len(results)
print(f"       {len(results)} kết quả, rớt {fail} ({rate:.1%})")
check("Tổng 1200 kết quả đã kết thúc", 1200, len(results))
check("Tỷ lệ rớt thực tế 10–25%", True, 0.10 <= rate <= 0.25)
low = [r for r in results if r["absentCount"] == 0]
high = [r for r in results if r["absentCount"] >= 4]
rate_low = sum(r["isPassed"] is False for r in low) / len(low)
rate_high = sum(r["isPassed"] is False for r in high) / len(high)
print(f"       Rớt khi không vắng buổi nào: {rate_low:.1%} ({len(low)}), vắng từ 4 buổi: {rate_high:.1%} ({len(high)})")
check("Vắng từ 4 buổi (>20% của 15) → luôn rớt", 1.0, rate_high)
check("Không vắng buổi nào → tỷ lệ rớt thấp (< 10%)", True, rate_low < 0.10)
mids = [r for r in results if r["midtermScore"] is not None]
hi = [r for r in mids if r["midtermScore"] >= 7.5]
lo = [r for r in mids if r["midtermScore"] < 4.5]
check("Giữa kỳ thấp (<4.5) rớt nhiều hơn giữa kỳ cao (>=7.5)", True,
      sum(r["isPassed"] is False for r in lo) / len(lo) > sum(r["isPassed"] is False for r in hi) / len(hi) + 0.3)
# Nhất quán công thức: điểm tổng đúng bằng tổng có trọng số
ok = 0
for r in results:
    exp = round(r["attendanceScore"] * 0.1 + r["assignmentScore"] * 0.2 + r["midtermScore"] * 0.2 + r["finalScore"] * 0.5 + 1e-9, 1)
    ok += abs(exp - r["totalScore"]) < 0.11
check("Điểm tổng khớp công thức trọng số cho 100% kết quả", len(results), ok)

print("== Cổng sinh viên ==")
mine_sv = call("GET", "/students/me/sections", token=sv1)[1]
check("SV001 học 15 lớp (3 kỳ × 5 môn)", 15, len(mine_sv))
check("SV001: 5 lớp học kỳ hiện tại chưa có điểm tổng", 5, sum(1 for x in mine_sv if x["semesterName"] == cur["name"] and x["totalScore"] is None))
check("SV001: 10 lớp đã kết thúc có điểm tổng", 10, sum(1 for x in mine_sv if x["semesterName"] != cur["name"] and x["totalScore"] is not None))
sections_sv90 = call("GET", "/students/me/sections", token=sv90)[1]
check("SV090 (khóa 2025) cũng học 15 lớp", 15, len(sections_sv90))
d = call("GET", f"/students/me/sections/{mine_sv[0]['sectionId']}", token=sv1)[1]
check("Chi tiết lớp hiện tại: có 8 buổi điểm danh", 8, len(d["attendance"]))
check("Tên sinh viên tiếng Việt hiển thị đúng", True, any(ord(c) > 127 for c in call("GET", "/auth/me", token=sv1)[1]["fullName"]))

sys.exit(0 if summary() else 1)
