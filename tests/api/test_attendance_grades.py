"""Test điểm danh, nhập điểm, tự tính điểm chuyên cần / tổng / qua rớt, và cổng sinh viên.

Chạy: python tests/api/test_attendance_grades.py
Các số liệu mong đợi được tính tay (xem chú thích từng bước).
"""
import sys
import time

from apitest_lib import *

RUN = str(int(time.time()))[-6:]
admin = login("admin", ENV["ADMIN_PASSWORD"])
gv1 = login("GV001", ENV["DEMO_PASSWORD"])
gv2 = login("GV002", ENV["DEMO_PASSWORD"])
assert admin and gv1 and gv2

# ---------- Dữ liệu nền: 1 môn, 1 học kỳ, 2 lớp (GV001 và GV002), 4 sinh viên ----------
cntt = next(d["id"] for d in call("GET", "/departments", token=admin)[1] if d["code"] == "CNTT")
lecs = {l["lecturerCode"]: l["id"] for l in call("GET", "/lecturers", token=admin)[1]}
course = call("POST", "/courses", {"code": f"AG{RUN}", "name": "Môn thử điểm", "credits": 3, "departmentId": cntt}, admin)[1]["id"]
sem = call("POST", "/semesters", {"name": f"HK-AG{RUN}", "startDate": "2031-01-01", "endDate": "2031-06-01", "isCurrent": False}, admin)[1]["id"]
W = {"attendanceWeight": 0.1, "assignmentWeight": 0.2, "midtermWeight": 0.2, "finalWeight": 0.5}
sec = call("POST", "/sections", {"code": f"AG{RUN}.01", "courseId": course, "semesterId": sem, "lecturerId": lecs["GV001"], "totalSessions": 10, "weights": W}, admin)[1]["id"]
sec2 = call("POST", "/sections", {"code": f"AG{RUN}.02", "courseId": course, "semesterId": sem, "lecturerId": lecs["GV002"], "totalSessions": 10, "weights": W}, admin)[1]["id"]
students = call("GET", "/students?q=SV00&pageSize=10", token=admin)[1]["items"]
by_code = {s["studentCode"]: s["id"] for s in students}
S1, S2, S3, S4, S5 = (by_code[f"SV00{i}"] for i in range(1, 6))
call("POST", f"/sections/{sec}/students", {"studentIds": [S1, S2, S3, S4]}, admin)
roster = {r["studentCode"]: r["enrollmentId"] for r in call("GET", f"/sections/{sec}/students", token=gv1)[1]}
E1, E2, E3, E4 = (roster[f"SV00{i}"] for i in range(1, 5))

print("== Điểm danh: xem ==")
s, sessions, _ = call("GET", f"/sections/{sec}/attendance", token=gv1)
check("GET attendance → 200", 200, s)
check("Đủ 10 buổi theo TotalSessions", 10, len(sessions))
check("Chưa buổi nào có ngày", 0, sum(1 for x in sessions if x["date"] is not None))
s, sheet, _ = call("GET", f"/sections/{sec}/attendance/1", token=gv1)
check("Buổi chưa điểm danh: 4 sinh viên", 4, len(sheet["entries"]))
check("Buổi chưa điểm danh: mặc định tất cả Present", {"Present"}, {e["status"] for e in sheet["entries"]})
check("GET buổi 11 (ngoài khoảng) → 404", 404, call("GET", f"/sections/{sec}/attendance/11", token=gv1)[0])
check("GET buổi 0 → 404", 404, call("GET", f"/sections/{sec}/attendance/0", token=gv1)[0])

print("== Điểm danh: ghi ==")
marks1 = [{"enrollmentId": E1, "status": "Present"}, {"enrollmentId": E2, "status": "Absent"},
          {"enrollmentId": E3, "status": "Late"}, {"enrollmentId": E4, "status": "Excused"}]
s, sheet, _ = call("PUT", f"/sections/{sec}/attendance/1", {"date": "2031-01-05", "marks": marks1}, gv1)
check("PUT buổi 1 → 200", 200, s)
check("Trạng thái đã lưu đúng", {E1: "Present", E2: "Absent", E3: "Late", E4: "Excused"}, {e["enrollmentId"]: e["status"] for e in sheet["entries"]})
check("Ngày đã lưu", "2031-01-05", sheet["date"])
# Buổi 2: SV4 không có trong danh sách → mặc định Present
s, sheet, _ = call("PUT", f"/sections/{sec}/attendance/2", {"date": "2031-01-12", "marks": [
    {"enrollmentId": E1, "status": "Present"}, {"enrollmentId": E2, "status": "Absent"}, {"enrollmentId": E3, "status": "Present"}]}, gv1)
check("Buổi 2: SV không nằm trong Marks mặc định Present", "Present", next(e["status"] for e in sheet["entries"] if e["enrollmentId"] == E4))

def grades(token=gv1, section=sec):
    return {r["studentCode"]: r for r in call("GET", f"/sections/{section}/grades", token=token)[1]["rows"]}

g = grades()
# Chuyên cần = 10 × (P + 0.5·L + E) / số buổi đã điểm danh
check("Chuyên cần SV001 (P,P) = 10.00", 10.0, g["SV001"]["attendanceScore"])
check("Chuyên cần SV002 (A,A) = 0.00", 0.0, g["SV002"]["attendanceScore"])
check("Chuyên cần SV003 (L,P) = 10×(0.5+1)/2 = 7.50", 7.5, g["SV003"]["attendanceScore"])
check("Chuyên cần SV004 (E,P) = 10.00", 10.0, g["SV004"]["attendanceScore"])
check("Số buổi vắng SV002 = 2", 2, g["SV002"]["absentCount"])
def grade_point(score):   # thang quy đổi của hệ thống: 8.5→4, 7→3, 5.5→2, 4→1, dưới 4→0
    return 4.0 if score >= 8.5 else 3.0 if score >= 7 else 2.0 if score >= 5.5 else 1.0 if score >= 4 else 0.0


def expected_gpa(token):
    """GPA tích lũy tính lại từ kết quả thật của các học kỳ đã kết thúc, theo tín chỉ."""
    credits = {c["name"]: c["credits"] for c in call("GET", "/courses?pageSize=100", token=admin)[1]["items"]}
    current_name = call("GET", "/semesters/current", token=admin)[1]["name"]
    points = weight = 0
    for item in call("GET", "/students/me/sections", token=token)[1]:
        if item["semesterName"] == current_name or item["totalScore"] is None:
            continue
        passed = call("GET", f"/students/me/sections/{item['sectionId']}", token=token)[1]["isPassed"]
        points += (grade_point(item["totalScore"]) if passed else 0) * credits[item["courseName"]]
        weight += credits[item["courseName"]]
    return round(points / weight, 2) if weight else None


sv_tokens = {code: login(code, ENV["DEMO_PASSWORD"]) for code in ("SV001", "SV002", "SV003", "SV004")}
for code in ("SV001", "SV003"):
    check(f"PriorGpa {code} = GPA tích lũy tính lại từ kết quả các học kỳ trước", expected_gpa(sv_tokens[code]), g[code]["priorGpa"])
check("PriorGpa nằm trong khoảng 0–4", True, all(0 <= g[c]["priorGpa"] <= 4 for c in ("SV001", "SV002", "SV003", "SV004")))

print("== Điểm danh: kiểm tra dữ liệu sai và quyền ==")
check("Marks trùng sinh viên → 400", 400, call("PUT", f"/sections/{sec}/attendance/3", {"date": "2031-01-19", "marks": [{"enrollmentId": E1, "status": "Present"}, {"enrollmentId": E1, "status": "Absent"}]}, gv1)[0])
check("EnrollmentId không thuộc lớp → 400", 400, call("PUT", f"/sections/{sec}/attendance/3", {"date": "2031-01-19", "marks": [{"enrollmentId": 99999, "status": "Present"}]}, gv1)[0])
check("Trạng thái sai chữ → 400", 400, call("PUT", f"/sections/{sec}/attendance/3", {"date": "2031-01-19", "marks": [{"enrollmentId": E1, "status": "Sai"}]}, gv1)[0])
check("Marks rỗng → 400", 400, call("PUT", f"/sections/{sec}/attendance/3", {"date": "2031-01-19", "marks": []}, gv1)[0])
check("Buổi 11 → 404", 404, call("PUT", f"/sections/{sec}/attendance/11", {"date": "2031-01-19", "marks": marks1}, gv1)[0])
check("Admin ghi điểm danh → 403 (chỉ Lecturer)", 403, call("PUT", f"/sections/{sec}/attendance/3", {"date": "2031-01-19", "marks": marks1}, admin)[0])
check("GV002 ghi điểm danh lớp của GV001 → 403", 403, call("PUT", f"/sections/{sec}/attendance/3", {"date": "2031-01-19", "marks": marks1}, gv2)[0])
check("GV002 xem điểm danh lớp của GV001 → 403", 403, call("GET", f"/sections/{sec}/attendance", token=gv2)[0])
check("Admin xem điểm danh → 200", 200, call("GET", f"/sections/{sec}/attendance", token=admin)[0])
sv1 = login("SV001", ENV["DEMO_PASSWORD"])
check("Sinh viên xem điểm danh cả lớp → 403", 403, call("GET", f"/sections/{sec}/attendance", token=sv1)[0])
check("Lớp không tồn tại → 404", 404, call("GET", "/sections/99999/attendance", token=gv1)[0])

s, sessions, _ = call("GET", f"/sections/{sec}/attendance", token=gv1)
b1 = sessions[0]
check("Tóm tắt buổi 1: 1 Present, 1 Absent, 1 Late, 1 Excused", (1, 1, 1, 1), (b1["present"], b1["absent"], b1["late"], b1["excused"]))
check("Buổi 1 và 2 đã có ngày, buổi 3 chưa", (True, True, True), (sessions[0]["date"] is not None, sessions[1]["date"] is not None, sessions[2]["date"] is None))

print("== Nhập điểm và tự tính ==")
check("GV001 xem bảng điểm → 200", 200, call("GET", f"/sections/{sec}/grades", token=gv1)[0])
rows = [{"enrollmentId": E1, "assignmentScore": 8, "midtermScore": 7, "finalScore": 9},
        {"enrollmentId": E2, "assignmentScore": 9, "midtermScore": 9, "finalScore": 9},
        {"enrollmentId": E3, "assignmentScore": 3, "midtermScore": 2, "finalScore": 2},
        {"enrollmentId": E4, "assignmentScore": 6, "midtermScore": 6}]       # SV004 chưa có điểm cuối kỳ
s, sheet, _ = call("PUT", f"/sections/{sec}/grades", {"rows": rows}, gv1)
check("PUT grades → 200", 200, s)
g = {r["studentCode"]: r for r in sheet["rows"]}
# SV001: 10×0.1 + 8×0.2 + 7×0.2 + 9×0.5 = 1 + 1.6 + 1.4 + 4.5 = 8.5 → qua
check("SV001 tổng = 8.5", 8.5, g["SV001"]["totalScore"])
check("SV001 qua môn", True, g["SV001"]["isPassed"])
# SV002: chuyên cần 0, vắng 2 buổi (≤ 20% × 10 = 2) → 0 + 1.8 + 1.8 + 4.5 = 8.1 → qua
check("SV002 tổng = 8.1", 8.1, g["SV002"]["totalScore"])
check("SV002 vắng 2/10 buổi (đúng 20%) vẫn qua", True, g["SV002"]["isPassed"])
# SV003: 7.5×0.1 + 3×0.2 + 2×0.2 + 2×0.5 = 0.75 + 0.6 + 0.4 + 1.0 = 2.75 → làm tròn 2.8 → rớt
check("SV003 tổng = 2.8 (làm tròn 1 chữ số)", 2.8, g["SV003"]["totalScore"])
check("SV003 điểm thấp → rớt", False, g["SV003"]["isPassed"])
check("SV004 chưa có điểm cuối kỳ → tổng null", None, g["SV004"]["totalScore"])
check("SV004 chưa có điểm cuối kỳ → IsPassed null", None, g["SV004"]["isPassed"])
check("SV004 lưu điểm bài tập = 6", 6.0, g["SV004"]["assignmentScore"])

print("== Rớt vì vắng quá 20% ==")
# Buổi 3: SV002 vắng → 3 buổi vắng > 2 → rớt dù điểm cao; SV001 vắng 1 buổi
s, _, _ = call("PUT", f"/sections/{sec}/attendance/3", {"date": "2031-01-19", "marks": [
    {"enrollmentId": E1, "status": "Absent"}, {"enrollmentId": E2, "status": "Absent"}]}, gv1)
check("PUT buổi 3 → 200", 200, s)
g = grades()
check("SV002 vắng 3/10 buổi > 20% → rớt dù tổng điểm cao", False, g["SV002"]["isPassed"])
check("SV002 tổng vẫn = 8.1 (chuyên cần 0)", 8.1, g["SV002"]["totalScore"])
# SV001: chuyên cần = 10×2/3 = 6.67 → tổng = 0.667 + 1.6 + 1.4 + 4.5 = 8.167 → 8.2
check("SV001 chuyên cần = 6.67 sau khi vắng buổi 3", 6.67, g["SV001"]["attendanceScore"])
check("SV001 tổng tự tính lại = 8.2", 8.2, g["SV001"]["totalScore"])
check("SV001 vắng 1 buổi vẫn qua", True, g["SV001"]["isPassed"])
check("Số buổi vắng SV001 = 1", 1, g["SV001"]["absentCount"])
# Ghi đè buổi 1: SV002 có mặt → vắng còn 2 → qua lại
call("PUT", f"/sections/{sec}/attendance/1", {"date": "2031-01-05", "marks": [{"enrollmentId": E2, "status": "Present"}]}, gv1)
check("Sửa buổi 1 cho SV002 có mặt → vắng 2 buổi → qua lại", True, grades()["SV002"]["isPassed"])

print("== Nhập điểm: kiểm tra dữ liệu sai và quyền ==")
check("Điểm 11 → 400", 400, call("PUT", f"/sections/{sec}/grades", {"rows": [{"enrollmentId": E1, "finalScore": 11}]}, gv1)[0])
check("Điểm âm → 400", 400, call("PUT", f"/sections/{sec}/grades", {"rows": [{"enrollmentId": E1, "midtermScore": -1}]}, gv1)[0])
check("Điểm 10.4 → 400 (không làm tròn xuống 10)", 400, call("PUT", f"/sections/{sec}/grades", {"rows": [{"enrollmentId": E1, "finalScore": 10.4}]}, gv1)[0])
check("Điểm 7.5 hợp lệ → 200", 200, call("PUT", f"/sections/{sec}/grades", {"rows": [{"enrollmentId": E4, "finalScore": 7.5}]}, gv1)[0])
check("EnrollmentId không thuộc lớp → 400", 400, call("PUT", f"/sections/{sec}/grades", {"rows": [{"enrollmentId": 99999, "finalScore": 5}]}, gv1)[0])
check("Trùng sinh viên → 400", 400, call("PUT", f"/sections/{sec}/grades", {"rows": [{"enrollmentId": E1, "finalScore": 5}, {"enrollmentId": E1, "finalScore": 6}]}, gv1)[0])
check("Rows rỗng → 400", 400, call("PUT", f"/sections/{sec}/grades", {"rows": []}, gv1)[0])
before = grades()["SV001"]["assignmentScore"]
call("PUT", f"/sections/{sec}/grades", {"rows": [{"enrollmentId": E1, "midtermScore": 7}]}, gv1)
check("Điểm null = giữ nguyên (không xóa điểm bài tập)", before, grades()["SV001"]["assignmentScore"])
check("Admin nhập điểm → 403", 403, call("PUT", f"/sections/{sec}/grades", {"rows": [{"enrollmentId": E1, "finalScore": 5}]}, admin)[0])
check("GV002 nhập điểm lớp của GV001 → 403", 403, call("PUT", f"/sections/{sec}/grades", {"rows": [{"enrollmentId": E1, "finalScore": 5}]}, gv2)[0])
check("Sinh viên nhập điểm → 403", 403, call("PUT", f"/sections/{sec}/grades", {"rows": [{"enrollmentId": E1, "finalScore": 5}]}, sv1)[0])
check("GV002 xem điểm lớp của GV001 → 403", 403, call("GET", f"/sections/{sec}/grades", token=gv2)[0])
check("Sinh viên xem bảng điểm cả lớp → 403", 403, call("GET", f"/sections/{sec}/grades", token=sv1)[0])
check("Admin xem bảng điểm → 200", 200, call("GET", f"/sections/{sec}/grades", token=admin)[0])
check("Lớp không tồn tại → 404", 404, call("GET", "/sections/99999/grades", token=gv1)[0])

print("== Sửa lớp khi đã điểm danh ==")
check("Giảm tổng số buổi xuống 2 (đã điểm danh tới buổi 3) → 400", 400,
      call("PUT", f"/sections/{sec}", {"code": f"AG{RUN}.01", "courseId": course, "semesterId": sem, "lecturerId": lecs["GV001"], "totalSessions": 2, "weights": W}, admin)[0])
check("Đổi tổng số buổi thành 20 → 200", 200,
      call("PUT", f"/sections/{sec}", {"code": f"AG{RUN}.01", "courseId": course, "semesterId": sem, "lecturerId": lecs["GV001"], "totalSessions": 20, "weights": W}, admin)[0])
check("Tổng số buổi 20 → tóm tắt có 20 buổi", 20, len(call("GET", f"/sections/{sec}/attendance", token=gv1)[1]))
# Với 20 buổi: ngưỡng vắng là 4 buổi → SV002 (2 buổi vắng) vẫn qua
call("PUT", f"/sections/{sec}", {"code": f"AG{RUN}.01", "courseId": course, "semesterId": sem, "lecturerId": lecs["GV001"], "totalSessions": 10, "weights": W}, admin)

print("== Cổng sinh viên ==")
s, mine, _ = call("GET", "/students/me/sections", token=sv1)
check("SV001 GET me/sections → 200", 200, s)
item = next((x for x in mine if x["sectionId"] == sec), None)
check("SV001 thấy lớp đã đăng ký", True, item is not None)
check("Số buổi vắng SV001 = 1, tổng buổi = 10", (1, 10), (item["absentCount"], item["totalSessions"]))
check("Điểm tổng SV001 = 8.2", 8.2, item["totalScore"])
check("Chưa dự đoán rủi ro → RiskLevel null", None, item["riskLevel"])
s, detail, _ = call("GET", f"/students/me/sections/{sec}", token=sv1)
check("GET me/sections/{id} → 200", 200, s)
check("Lịch sử điểm danh gồm 3 buổi, đúng thứ tự", [1, 2, 3], [a["sessionNo"] for a in detail["attendance"]])
check("Lịch sử điểm danh SV001: Present, Present, Absent", ["Present", "Present", "Absent"], [a["status"] for a in detail["attendance"]])
check("Trọng số trả về đúng", 0.5, detail["weights"]["finalWeight"])
check("Điểm thành phần: bài tập 8, giữa kỳ 7, cuối kỳ 9", (8.0, 7.0, 9.0), (detail["assignmentScore"], detail["midtermScore"], detail["finalScore"]))
check("Lớp không đăng ký → 404", 404, call("GET", f"/students/me/sections/{sec2}", token=sv1)[0])
sv5 = login("SV005", ENV["DEMO_PASSWORD"])
check("SV005 (chưa đăng ký) không thấy lớp", False, any(x["sectionId"] == sec for x in call("GET", "/students/me/sections", token=sv5)[1]))
check("Admin gọi me/sections → 403", 403, call("GET", "/students/me/sections", token=admin)[0])
check("Giảng viên gọi me/sections → 403", 403, call("GET", "/students/me/sections", token=gv1)[0])
check("Không token → 401", 401, call("GET", "/students/me/sections")[0])

print("== Xóa sinh viên khỏi lớp xóa luôn điểm danh ==")
check("Unenroll SV003 → 204", 204, call("DELETE", f"/sections/{sec}/students/{E3}", token=admin)[0])
check("Sau unenroll: lớp còn 3 sinh viên", 3, len(call("GET", f"/sections/{sec}/students", token=gv1)[1]))
check("Sau unenroll: bảng điểm chỉ còn 3 dòng", 3, len(grades()))

print("== Dọn dữ liệu thử ==")
for section in (sec, sec2):
    for r in call("GET", f"/sections/{section}/students", token=admin)[1]:
        call("DELETE", f"/sections/{section}/students/{r['enrollmentId']}", token=admin)
check("Xóa lớp 1 (còn buổi điểm danh sót) → 204", 204, call("DELETE", f"/sections/{sec}", token=admin)[0])
check("Xóa lớp 2 → 204", 204, call("DELETE", f"/sections/{sec2}", token=admin)[0])
check("Xóa môn → 204", 204, call("DELETE", f"/courses/{course}", token=admin)[0])
check("Xóa học kỳ → 204", 204, call("DELETE", f"/semesters/{sem}", token=admin)[0])

sys.exit(0 if summary() else 1)
