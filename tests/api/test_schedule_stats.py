"""Test thời khóa biểu, lịch học, lịch/điểm thi, bảng điểm (GPA), phòng học và thống kê tổng quan.

Chạy: python tests/api/test_schedule_stats.py   (cần dữ liệu demo; dùng database mới để các số khớp)
"""
import time
from datetime import date, timedelta
from decimal import Decimal, ROUND_HALF_UP

from apitest_lib import *

RUN = str(int(time.time()))[-6:]
admin = login("admin", ENV["ADMIN_PASSWORD"])
gv1 = login("GV001", ENV["DEMO_PASSWORD"])
sv = login("SV001", ENV["DEMO_PASSWORD"])
assert admin and gv1 and sv, "Không đăng nhập được các tài khoản demo"


def hhmm(t):
    return t[:5]


def point(total):
    total = Decimal(str(total))
    return Decimal("4") if total >= Decimal("8.5") else Decimal("3") if total >= 7 else Decimal("2") if total >= Decimal("5.5") else Decimal("1") if total >= 4 else Decimal("0")


def gpa(courses):
    """GPA thang 4 có trọng số tín chỉ, môn không đạt tính 0, làm tròn half-up 2 chữ số."""
    credits = sum(c["credits"] for c in courses)
    pts = sum((point(c["totalScore"]) if c["result"] == "Đạt" else Decimal(0)) * c["credits"] for c in courses)
    return float((pts / credits).quantize(Decimal("0.01"), ROUND_HALF_UP))


print("== Phòng học ==")
rooms = call("GET", "/rooms", token=sv)[1]
check("Sinh viên xem được danh sách phòng", 12, len(rooms))
check("Phòng có tòa và lầu", True, all(r["building"] in ("A1", "A2", "B1") and 1 <= r["floor"] <= 4 for r in rooms))
check("Lọc phòng theo tòa A2", 4, len(call("GET", "/rooms?building=A2", token=sv)[1]))
check("Sinh viên thêm phòng → 403", 403, call("POST", "/rooms", {"code": "X", "building": "A1", "floor": 1, "capacity": 10}, sv)[0])
check("Không đăng nhập → 401", 401, call("GET", "/rooms")[0])
body = {"code": f"T{RUN}", "building": "T9", "floor": 2, "capacity": 40}
s, r1, _ = call("POST", "/rooms", body, admin)
check("Admin thêm phòng → 201", 201, s)
check("Thêm phòng trùng mã → 409", 409, call("POST", "/rooms", body, admin)[0])
check("Thêm phòng sức chứa 0 → 400", 400, call("POST", "/rooms", {**body, "code": f"U{RUN}", "capacity": 0}, admin)[0])
s, r2, _ = call("POST", "/rooms", {**body, "code": f"V{RUN}", "floor": 3}, admin)
check("Thêm phòng thứ hai → 201", 201, s)
s, upd, _ = call("PUT", f"/rooms/{r1['id']}", {**body, "floor": 5}, admin)
check("Sửa lầu của phòng → 200", (200, 5), (s, upd["floor"]))
check("Sửa thành mã phòng đã có → 409", 409, call("PUT", f"/rooms/{r1['id']}", {**body, "code": f"V{RUN}"}, admin)[0])
check("Sửa phòng không tồn tại → 404", 404, call("PUT", "/rooms/99999", body, admin)[0])

print("== Xếp lịch cho lớp học phần ==")
course_id = call("GET", "/courses?pageSize=1", token=admin)[1]["items"][0]["id"]
cur = call("GET", "/semesters/current", token=admin)[1]
lecs = call("GET", "/lecturers", token=admin)[1]
lid1, lid2 = lecs[0]["id"], lecs[1]["id"]
w = {"attendanceWeight": 0.1, "assignmentWeight": 0.2, "midtermWeight": 0.2, "finalWeight": 0.5}
# Thứ 7 (7), tiết 1-2: demo chỉ dùng thứ 2..6 nên không đụng lớp có sẵn
sched = {"weekday": 7, "startPeriod": 1, "periodCount": 2, "roomId": r1["id"], "examDate": str(date(2027, 1, 9)), "examStartTime": "08:00:00", "examRoomId": r2["id"]}
base = {"code": f"L{RUN}.A", "courseId": course_id, "semesterId": cur["id"], "lecturerId": lid1, "totalSessions": 15, "weights": w}
s, sec, _ = call("POST", "/sections", {**base, "schedule": sched}, admin)
check("Tạo lớp có lịch học và lịch thi → 201", 201, s)
sc = sec["schedule"]
check("Giờ học: tiết 1-2 = 07:00 – 08:35", ("07:00", "08:35"), (hhmm(sc["startTime"]), hhmm(sc["endTime"])))
check("Phòng học trả kèm tòa, lầu", (f"T{RUN}", "T9", 5), (sc["roomCode"], sc["building"], sc["floor"]))
check("Lịch thi trả kèm phòng thi", ("2027-01-09", "08:00", f"V{RUN}", 3), (sc["examDate"], hhmm(sc["examStartTime"]), sc["examRoomCode"], sc["examFloor"]))
check("GET lớp trả lại lịch", sc, call("GET", f"/sections/{sec['id']}", token=admin)[1]["schedule"])

second = {**base, "code": f"L{RUN}.B", "lecturerId": lid2}
check("Trùng phòng cùng thứ, giao tiết → 409", 409, call("POST", "/sections", {**second, "schedule": {**sched, "startPeriod": 2}}, admin)[0])
check("Trùng giảng viên cùng thứ, giao tiết → 409", 409,
      call("POST", "/sections", {**second, "lecturerId": lid1, "schedule": {**sched, "roomId": r2["id"]}}, admin)[0])
s, ok2, _ = call("POST", "/sections", {**second, "schedule": {**sched, "startPeriod": 3}}, admin)
check("Cùng phòng nhưng tiết liền kề (không giao) → 201", 201, s)
check("Lịch học thiếu trường (chỉ có Weekday) → 400", 400, call("POST", "/sections", {**second, "code": f"L{RUN}.C", "schedule": {"weekday": 7}}, admin)[0])
check("Tiết vượt quá 12 → 400", 400, call("POST", "/sections", {**second, "code": f"L{RUN}.C", "schedule": {**sched, "startPeriod": 12, "periodCount": 2}}, admin)[0])
check("Thứ không hợp lệ (9) → 400", 400, call("POST", "/sections", {**second, "code": f"L{RUN}.C", "schedule": {**sched, "weekday": 9}}, admin)[0])
check("Phòng học không tồn tại → 400", 400, call("POST", "/sections", {**second, "code": f"L{RUN}.C", "schedule": {**sched, "roomId": 99999}}, admin)[0])
check("Có giờ thi nhưng thiếu ngày thi → 400", 400,
      call("POST", "/sections", {**second, "code": f"L{RUN}.C", "schedule": {**sched, "examDate": None}}, admin)[0])
s, bare, _ = call("POST", "/sections", {**second, "code": f"L{RUN}.D"}, admin)
check("Lớp không xếp lịch vẫn tạo được (Schedule bỏ trống)", (201, None), (s, bare["schedule"]))
call("DELETE", f"/sections/{bare['id']}", token=admin)
check("Xóa phòng đang dùng → 409", 409, call("DELETE", f"/rooms/{r1['id']}", token=admin)[0])
check("Giảng viên sửa lớp → 403", 403, call("PUT", f"/sections/{sec['id']}", {**base, "schedule": sched}, gv1)[0])
s, cleared, _ = call("PUT", f"/sections/{sec['id']}", base, admin)
check("PUT không có Schedule → xóa lịch (schedule = null)", (200, None), (s, cleared["schedule"]))
call("DELETE", f"/sections/{sec['id']}", token=admin)
call("DELETE", f"/sections/{ok2['id']}", token=admin)
check("Xóa phòng sau khi gỡ lớp → 204", 204, call("DELETE", f"/rooms/{r1['id']}", token=admin)[0])
check("Xóa phòng thứ hai → 204", 204, call("DELETE", f"/rooms/{r2['id']}", token=admin)[0])
check("Xóa phòng không tồn tại → 404", 404, call("DELETE", f"/rooms/{r1['id']}", token=admin)[0])

print("== Thời khóa biểu (SV001) ==")
s, tt, _ = call("GET", "/students/me/timetable", token=sv)
check("GET timetable → 200", 200, s)
check("Mặc định là học kỳ hiện tại", cur["name"], tt["semesterName"])
check("Năm học HK1 2026-2027 = 2026-2027", "2026-2027", tt["academicYear"])
items = tt["items"]
check("5 môn có lịch", 5, len(items))
check("Sắp theo thứ rồi tiết", sorted(items, key=lambda i: (i["weekday"], i["startPeriod"])), items)
check("Mỗi môn có phòng, tòa, lầu", True, all(i["roomCode"] and i["building"] in ("A1", "A2", "B1") and 1 <= i["floor"] <= 4 for i in items))
check("Mã phòng bắt đầu bằng tên tòa", True, all(i["roomCode"].startswith(i["building"]) for i in items))
check("Thứ trong khoảng 2..6", True, all(2 <= i["weekday"] <= 6 for i in items))
check("Mỗi ca dài 3 tiết, bắt đầu tại tiết 1/4/7/10", True, all(i["periodCount"] == 3 and i["startPeriod"] in (1, 4, 7, 10) for i in items))
check("Giờ học khớp tiết (tiết 1 = 07:00 – 09:25)", True,
      all((hhmm(i["startTime"]), hhmm(i["endTime"])) == ("07:00", "09:25") for i in items if i["startPeriod"] == 1))
check("Không trùng giờ giữa các môn", len({(i["weekday"], i["startPeriod"]) for i in items}), len(items))
s, old, _ = call("GET", "/students/me/timetable?semesterId=1", token=sv)
check("Thời khóa biểu học kỳ cũ (semesterId=1)", ("HK1 2025-2026", 5), (old["semesterName"], len(old["items"])))
check("semesterId không tồn tại → 404", 404, call("GET", "/students/me/timetable?semesterId=99999", token=sv)[0])

print("== Lịch học theo ngày ==")
s, week, _ = call("GET", "/students/me/schedule?from=2026-11-09&to=2026-11-15", token=sv)
check("GET schedule một tuần → 200, 5 buổi", (200, 5), (s, len(week)))
check("Ngày nằm trong khoảng yêu cầu", True, all("2026-11-09" <= i["date"] <= "2026-11-15" for i in week))
check("Thứ của buổi khớp ngày", True, all(date.fromisoformat(i["date"]).isoweekday() + 1 == i["weekday"] for i in week))
check("Sắp theo ngày", sorted(week, key=lambda i: i["date"]), week)
check("Ngày 09/11/2026 là buổi thứ 10 của học kỳ", {10}, {i["sessionNo"] for i in week})
sections = {i["sectionId"]: i for i in call("GET", "/students/me/sections", token=sv)[1]}
cross_ok = True
for item in items:
    detail = call("GET", f"/students/me/sections/{item['sectionId']}", token=sv)[1]
    dates = {a["sessionNo"]: a["date"] for a in detail["attendance"]}
    got = {i["sessionNo"]: i["date"] for i in call("GET", f"/students/me/schedule?from=2026-09-07&to=2026-12-06", token=sv)[1] if i["sectionId"] == item["sectionId"]}
    cross_ok &= all(got.get(n) == d for n, d in dates.items())
check("Ngày trong lịch học khớp ngày các buổi đã điểm danh", True, cross_ok)
full = call("GET", "/students/me/schedule?from=2026-09-07&to=2026-12-06", token=sv)[1]
check("Mỗi lớp tối đa 15 buổi trong lịch", True, all(sum(1 for i in full if i["sectionId"] == sid) <= 15 for sid in sections))
check("Ngoài học kỳ không có buổi nào", [], call("GET", "/students/me/schedule?from=2027-02-01&to=2027-02-07", token=sv)[1])
check("to < from → 400", 400, call("GET", "/students/me/schedule?from=2026-11-15&to=2026-11-09", token=sv)[0])
check("Khoảng quá 92 ngày → 400", 400, call("GET", "/students/me/schedule?from=2026-01-01&to=2026-12-31", token=sv)[0])
check("Không truyền tham số (tuần hiện tại) → 200", 200, call("GET", "/students/me/schedule", token=sv)[0])

print("== Điểm thi và lịch thi ==")
s, exams, _ = call("GET", "/students/me/exams", token=sv)
check("GET exams → 200, 15 môn (5 × 3 học kỳ)", (200, 15), (s, len(exams)))
check("Mới nhất trước", True, exams[0]["semesterName"] == "HK1 2026-2027" and exams[-1]["semesterName"] == "HK1 2025-2026")
check("Có lịch thi: ngày, giờ, phòng, tòa, lầu", True, all(e["examDate"] and e["examStartTime"] and e["roomCode"] and e["building"] and e["floor"] for e in exams))
check("Không xếp ngày thi vào chủ nhật", True, all(date.fromisoformat(e["examDate"]).isoweekday() != 7 for e in exams))
finished = [e for e in exams if e["semesterName"] != "HK1 2026-2027"]
check("Học kỳ đã kết thúc có điểm thi và kết quả", True, all(e["finalScore"] is not None and e["isPassed"] is not None for e in finished))
check("Học kỳ hiện tại chưa có điểm thi", True, all(e["finalScore"] is None and e["isPassed"] is None for e in exams if e["semesterName"] == "HK1 2026-2027"))
check("Lọc exams theo học kỳ", 5, len(call("GET", "/students/me/exams?semesterId=2", token=sv)[1]))

print("== Bảng điểm và GPA ==")
s, tr, _ = call("GET", "/students/me/transcript", token=sv)
check("GET transcript → 200", 200, s)
check("Đúng mã sinh viên", "SV001", tr["studentCode"])
check("3 học kỳ, mới nhất trước", ["HK1 2026-2027", "HK2 2025-2026", "HK1 2025-2026"], [x["semesterName"] for x in tr["semesters"]])
now, *past = tr["semesters"]
check("Học kỳ hiện tại: đang học, GPA null, 15 tín chỉ đăng ký", (False, None, 15), (now["isFinished"], now["gpa"], now["creditsRegistered"]))
check("Môn đang học: Result = Đang học, chưa có điểm quy đổi", True, all(c["result"] == "Đang học" and c["gradePoint"] is None for c in now["courses"]))
check("Học kỳ đã kết thúc: isFinished", True, all(x["isFinished"] for x in past))
check("GPA từng học kỳ khớp công thức", [gpa(x["courses"]) for x in past], [x["gpa"] for x in past])
allc = [c for x in past for c in x["courses"]]
check("GPA tích lũy khớp công thức", gpa(allc), tr["cumulativeGpa"])
check("Tín chỉ đạt tích lũy", sum(c["credits"] for c in allc if c["result"] == "Đạt"), tr["creditsPassed"])
check("Điểm quy đổi từng môn đúng thang 4", True,
      all(c["gradePoint"] == float(point(c["totalScore"]) if c["result"] == "Đạt" else 0) for c in allc))
check("Môn không đạt có điểm quy đổi 0", True, all(c["gradePoint"] == 0 for c in allc if c["result"] == "Không đạt"))
check("Năm học 2025-2026 gộp 2 học kỳ", ["2025-2026"], [y["academicYear"] for y in tr["years"]])
check("GPA năm học khớp công thức", gpa(allc), tr["years"][0]["gpa"])
check("Bảng điểm có đủ điểm thành phần", True, all(c["attendanceScore"] is not None and c["finalScore"] is not None for c in allc))
sv90 = login("SV090", ENV["DEMO_PASSWORD"])
tr90 = call("GET", "/students/me/transcript", token=sv90)[1]
check("Sinh viên khóa 2025 vẫn có bảng điểm 3 kỳ", 3, len(tr90["semesters"]))

print("== Phân quyền cổng sinh viên ==")
for path in ("timetable", "schedule", "exams", "transcript"):
    check(f"Giảng viên gọi /students/me/{path} → 403", 403, call("GET", f"/students/me/{path}", token=gv1)[0])
    check(f"Admin gọi /students/me/{path} → 403", 403, call("GET", f"/students/me/{path}", token=admin)[0])
    check(f"Không đăng nhập gọi /students/me/{path} → 401", 401, call("GET", f"/students/me/{path}")[0])

print("== Thống kê tổng quan ==")
s, trend, _ = call("GET", "/stats/trend", token=admin)
check("GET stats/trend → 200", 200, s)
years = trend["years"]
check("2 năm học, mới nhất trước", ["2026-2027", "2025-2026"], [y["academicYear"] for y in years])
y2526 = years[1]
check("Năm 2025-2026 có 2 học kỳ", 2, len(y2526["semesters"]))
check("Năm 2025-2026: 1200 kết quả (120 SV × 5 môn × 2 kỳ)", 1200, y2526["graded"])
check("Học kỳ hiện tại chưa có kết quả → điểm/GPA/tỷ lệ đạt null", (0, None, None, None),
      (years[0]["graded"], years[0]["averageScore"], years[0]["averageGpa"], years[0]["passRate"]))
check("Tỷ lệ đạt trong khoảng hợp lý (60%–95%)", True, 0.6 < y2526["passRate"] < 0.95)
check("GPA trung bình trong 0..4", True, 0 <= y2526["averageGpa"] <= 4)
check("Tỷ lệ đạt năm = (đạt HK1 + đạt HK2) / tổng", round((sum(round(s_["passRate"] * s_["graded"]) for s_ in y2526["semesters"])) / y2526["graded"], 4), y2526["passRate"])
check("Mỗi học kỳ 16 lớp, 120 sinh viên, 600 đăng ký", {(16, 120, 600)}, {(x["sections"], x["students"], x["enrollments"]) for y in years for x in y["semesters"]})

s, ov, _ = call("GET", "/stats/overview?semesterId=2", token=admin)
check("GET stats/overview?semesterId=2 → 200", 200, s)
check("Tổng quan đúng học kỳ", "HK2 2025-2026", ov["summary"]["semesterName"])
check("Phân bố điểm cộng đủ số kết quả", ov["summary"]["graded"], sum(b["count"] for b in ov["scoreDistribution"]))
check("Phân bố GPA cộng đủ 120 sinh viên", 120, sum(b["count"] for b in ov["gpaDistribution"]))
check("Rủi ro: tổng = số đăng ký", 600, sum(ov["risk"].values()))
check("16 môn trong thống kê theo môn", 16, len(ov["courses"]))
check("Tổng kết quả theo môn = tổng", ov["summary"]["graded"], sum(c["graded"] for c in ov["courses"]))
check("Thống kê theo lớp sinh hoạt có đủ 120 sinh viên", 120, sum(c["students"] for c in ov["classes"]))
check("Không truyền semesterId → học kỳ hiện tại", cur["name"], call("GET", "/stats/overview", token=admin)[1]["summary"]["semesterName"])
check("semesterId không tồn tại → 404", 404, call("GET", "/stats/overview?semesterId=99999", token=admin)[0])
for path in ("trend", "overview"):
    check(f"Giảng viên xem stats/{path} → 403", 403, call("GET", f"/stats/{path}", token=gv1)[0])
    check(f"Sinh viên xem stats/{path} → 403", 403, call("GET", f"/stats/{path}", token=sv)[0])
    check(f"Không đăng nhập xem stats/{path} → 401", 401, call("GET", f"/stats/{path}")[0])

import sys
sys.exit(0 if summary() else 1)
