"""Test quản trị: tài khoản, khoa, môn, học kỳ, lớp học phần, thêm sinh viên vào lớp, import Excel, đổi mật khẩu.

Chạy: python tests/api/test_admin.py   (cần: pip install openpyxl)
"""
import io
import sys
import time

import openpyxl

from apitest_lib import *

RUN = str(int(time.time()))[-6:]   # hậu tố riêng mỗi lần chạy để không trùng dữ liệu
admin = login("admin", ENV["ADMIN_PASSWORD"])
gv1 = login("GV001", ENV["DEMO_PASSWORD"])
gv2 = login("GV002", ENV["DEMO_PASSWORD"])
sv = login("SV001", ENV["DEMO_PASSWORD"])
assert admin and gv1 and gv2 and sv, "Không đăng nhập được các tài khoản demo"

print("== Khoa ==")
s, depts, _ = call("GET", "/departments", token=admin)
check("GET /departments: có khoa CNTT", True, any(d["code"] == "CNTT" for d in depts))
cntt = next(d for d in depts if d["code"] == "CNTT")["id"]
s, d, _ = call("POST", "/departments", {"code": f"T{RUN}", "name": "Khoa thử"}, admin)
check("POST /departments → 201", 201, s)
dept_id = d["id"]
check("POST /departments trùng mã → 409", 409, call("POST", "/departments", {"code": f"T{RUN}", "name": "x"}, admin)[0])
check("POST /departments thiếu mã → 400", 400, call("POST", "/departments", {"code": "", "name": "x"}, admin)[0])
check("PUT /departments → 200 (đổi tên)", "Khoa đã đổi", call("PUT", f"/departments/{dept_id}", {"code": f"T{RUN}", "name": "Khoa đã đổi"}, admin)[1]["name"])
check("PUT /departments không có → 404", 404, call("PUT", "/departments/99999", {"code": "Z", "name": "x"}, admin)[0])
check("DELETE /departments còn dữ liệu (CNTT) → 409", 409, call("DELETE", f"/departments/{cntt}", token=admin)[0])
check("POST /departments bằng Student → 403", 403, call("POST", "/departments", {"code": "ZZ", "name": "x"}, sv)[0])

print("== Môn học ==")
s, c, _ = call("POST", "/courses", {"code": f"C{RUN}", "name": "Môn thử", "credits": 3, "departmentId": cntt}, admin)
check("POST /courses → 201", 201, s)
course_id = c["id"]
check("POST /courses trùng mã → 409", 409, call("POST", "/courses", {"code": f"C{RUN}", "name": "x", "credits": 3, "departmentId": cntt}, admin)[0])
check("POST /courses khoa không tồn tại → 400", 400, call("POST", "/courses", {"code": "NOPE", "name": "x", "credits": 3, "departmentId": 99999}, admin)[0])
check("POST /courses tín chỉ 0 → 400", 400, call("POST", "/courses", {"code": "NOPE2", "name": "x", "credits": 0, "departmentId": cntt}, admin)[0])
s, page, _ = call("GET", f"/courses?q=C{RUN}", token=admin)
check("GET /courses?q= tìm đúng 1 môn", 1, page["totalItems"])
check("PUT /courses → tên đổi", "Môn thử 2", call("PUT", f"/courses/{course_id}", {"code": f"C{RUN}", "name": "Môn thử 2", "credits": 4, "departmentId": cntt}, admin)[1]["name"])

print("== Học kỳ ==")
s, sem1, _ = call("POST", "/semesters", {"name": f"HK-A{RUN}", "startDate": "2030-01-01", "endDate": "2030-05-01", "isCurrent": False}, admin)
check("POST /semesters → 201", 201, s)
check("POST /semesters ngày kết thúc trước bắt đầu → 400", 400, call("POST", "/semesters", {"name": "x", "startDate": "2030-05-01", "endDate": "2030-01-01", "isCurrent": False}, admin)[0])
s, cur0, _ = call("GET", "/semesters/current", token=admin)
old_current = cur0["id"] if s == 200 else None
s, sem2, _ = call("POST", "/semesters", {"name": f"HK-B{RUN}", "startDate": "2030-06-01", "endDate": "2030-10-01", "isCurrent": True}, admin)
check("POST /semesters isCurrent=true → 201", 201, s)
check("GET /semesters/current = học kỳ vừa đặt", sem2["id"], call("GET", "/semesters/current", token=admin)[1]["id"])
all_sem = call("GET", "/semesters", token=admin)[1]
check("Chỉ đúng 1 học kỳ hiện tại", 1, sum(x["isCurrent"] for x in all_sem))
s, _, _ = call("PUT", f"/semesters/{sem1['id']}", {"name": f"HK-A{RUN}", "startDate": "2030-01-01", "endDate": "2030-05-01", "isCurrent": True}, admin)
check("PUT /semesters isCurrent=true → 200", 200, s)
check("Sau PUT vẫn chỉ 1 học kỳ hiện tại", 1, sum(x["isCurrent"] for x in call("GET", "/semesters", token=admin)[1]))

print("== Người dùng ==")
body = {"username": f"gvt{RUN}", "password": "MatKhau123", "fullName": "GV Thử", "email": f"gvt{RUN}@t.vn", "role": "Lecturer", "lecturer": {"lecturerCode": f"GT{RUN}", "departmentId": cntt}}
s, lec, _ = call("POST", "/users", body, admin)
check("POST /users giảng viên → 201", 201, s)
lec_user_id = lec["id"]
check("POST /users trùng username → 409", 409, call("POST", "/users", {**body, "email": "khac@t.vn", "lecturer": {"lecturerCode": "KHAC1", "departmentId": cntt}}, admin)[0])
check("POST /users trùng email → 409", 409, call("POST", "/users", {**body, "username": f"khac{RUN}", "lecturer": {"lecturerCode": "KHAC2", "departmentId": cntt}}, admin)[0])
check("POST /users trùng mã GV → 409", 409, call("POST", "/users", {**body, "username": f"khac2{RUN}", "email": "k2@t.vn"}, admin)[0])
check("POST /users mật khẩu ngắn → 400", 400, call("POST", "/users", {**body, "username": "u1", "password": "123"}, admin)[0])
check("POST /users Student thiếu hồ sơ → 400", 400, call("POST", "/users", {"username": "u2", "password": "MatKhau123", "fullName": "A", "role": "Student"}, admin)[0])
check("POST /users khoa không tồn tại → 400", 400, call("POST", "/users", {"username": "u3", "password": "MatKhau123", "fullName": "A", "role": "Student", "student": {"studentCode": "X1", "departmentId": 99999, "className": "L", "enrollmentYear": 2025}}, admin)[0])
s, stu, _ = call("POST", "/users", {"username": f"svt{RUN}", "password": "MatKhau123", "fullName": "SV Thử", "role": "Student", "student": {"studentCode": f"ST{RUN}", "departmentId": cntt, "className": "CNTT-T", "enrollmentYear": 2026}}, admin)
check("POST /users sinh viên → 201", 201, s)
stu_user_id = stu["id"]
check("Đăng nhập tài khoản vừa tạo", True, login(f"svt{RUN}", "MatKhau123") is not None)
check("GET /users/{id} có hồ sơ sinh viên", f"ST{RUN}", call("GET", f"/users/{stu_user_id}", token=admin)[1]["student"]["studentCode"])
check("GET /users?role=Lecturer&q= lọc đúng", 1, call("GET", f"/users?role=Lecturer&q=gvt{RUN}", token=admin)[1]["totalItems"])
check("GET /users/99999 → 404", 404, call("GET", "/users/99999", token=admin)[0])
s, upd, _ = call("PUT", f"/users/{stu_user_id}", {"fullName": "SV Thử Sửa", "email": None, "isActive": True, "student": {"studentCode": f"ST{RUN}", "departmentId": cntt, "className": "CNTT-T2", "enrollmentYear": 2026}}, admin)
check("PUT /users sửa lớp sinh hoạt", "CNTT-T2", upd["student"]["className"])
me_admin = call("GET", "/auth/me", token=admin)[1]
check("PUT /users tự khóa mình → 400", 400, call("PUT", f"/users/{me_admin['id']}", {"fullName": "Quản trị viên", "email": None, "isActive": False}, admin)[0])
# Khóa tài khoản: token cũ phải hết hiệu lực ngay
tok_stu = login(f"svt{RUN}", "MatKhau123")
check("Trước khi khóa: /auth/me = 200", 200, call("GET", "/auth/me", token=tok_stu)[0])
call("PUT", f"/users/{stu_user_id}", {"fullName": "SV Thử Sửa", "email": None, "isActive": False}, admin)
check("Sau khi khóa: token cũ → 401", 401, call("GET", "/auth/me", token=tok_stu)[0])
check("Sau khi khóa: đăng nhập lại bị từ chối", None, login(f"svt{RUN}", "MatKhau123"))
check("POST reset-password → 204", 204, call("POST", f"/users/{lec_user_id}/reset-password", {"newPassword": "MatKhauMoi999"}, admin)[0])
check("Đăng nhập bằng mật khẩu mới", True, login(f"gvt{RUN}", "MatKhauMoi999") is not None)
check("Đăng nhập bằng mật khẩu cũ bị từ chối", None, login(f"gvt{RUN}", "MatKhau123"))
check("GET /users bằng Lecturer → 403", 403, call("GET", "/users", token=gv1)[0])

print("== Danh sách sinh viên / giảng viên ==")
s, p, _ = call("GET", "/students?q=SV00", token=admin)
check("GET /students?q=SV00 có kết quả", True, p["totalItems"] >= 1)
check("GET /students không gồm SV bị khóa", 0, call("GET", f"/students?q=ST{RUN}", token=admin)[1]["totalItems"])
lecs = call("GET", "/lecturers", token=admin)[1]
check("GET /lecturers có GV001", True, any(l["lecturerCode"] == "GV001" for l in lecs))
lid1 = next(l["id"] for l in lecs if l["lecturerCode"] == "GV001")
lid2 = next(l["id"] for l in lecs if l["lecturerCode"] == "GV002")

print("== Lớp học phần ==")
w = {"attendanceWeight": 0.1, "assignmentWeight": 0.2, "midtermWeight": 0.2, "finalWeight": 0.5}
sec_body = {"code": f"S{RUN}.01", "courseId": course_id, "semesterId": sem1["id"], "lecturerId": lid1, "totalSessions": 15, "weights": w}
s, sec, _ = call("POST", "/sections", sec_body, admin)
check("POST /sections → 201", 201, s)
sec_id = sec["id"]
check("POST /sections trùng mã → 409", 409, call("POST", "/sections", sec_body, admin)[0])
check("POST /sections tổng trọng số ≠ 1 → 400", 400, call("POST", "/sections", {**sec_body, "code": "X1", "weights": {**w, "finalWeight": 0.6}}, admin)[0])
check("POST /sections môn không tồn tại → 400", 400, call("POST", "/sections", {**sec_body, "code": "X2", "courseId": 99999}, admin)[0])
check("POST /sections giảng viên không tồn tại → 400", 400, call("POST", "/sections", {**sec_body, "code": "X3", "lecturerId": 99999}, admin)[0])
check("POST /sections bằng Lecturer → 403", 403, call("POST", "/sections", sec_body, gv1)[0])
s, other, _ = call("POST", "/sections", {**sec_body, "code": f"S{RUN}.02", "lecturerId": lid2}, admin)
other_id = other["id"]
ids_admin = {x["id"] for x in call("GET", f"/sections?courseId={course_id}", token=admin)[1]["items"]}
check("Admin thấy cả 2 lớp", {sec_id, other_id}, ids_admin)
ids_gv1 = {x["id"] for x in call("GET", f"/sections?courseId={course_id}", token=gv1)[1]["items"]}
check("GV001 chỉ thấy lớp của mình", {sec_id}, ids_gv1)
check("GV001 xem lớp của GV002 → 403", 403, call("GET", f"/sections/{other_id}", token=gv1)[0])
check("GV001 xem lớp của mình → 200", 200, call("GET", f"/sections/{sec_id}", token=gv1)[0])
check("GET /sections/99999 → 404", 404, call("GET", "/sections/99999", token=admin)[0])
check("SV chưa đăng ký xem lớp → 403", 403, call("GET", f"/sections/{sec_id}", token=sv)[0])

print("== Thêm sinh viên vào lớp ==")
stu_ids = [x["id"] for x in call("GET", "/students?q=SV00&pageSize=3", token=admin)[1]["items"]]
s, r, _ = call("POST", f"/sections/{sec_id}/students", {"studentIds": stu_ids}, admin)
check("POST /sections/{id}/students → thêm đủ", (len(stu_ids), 0), (r["enrolled"], r["alreadyEnrolled"]))
s, r, _ = call("POST", f"/sections/{sec_id}/students", {"studentIds": stu_ids}, admin)
check("Thêm lần 2 → bỏ qua hết", (0, len(stu_ids)), (r["enrolled"], r["alreadyEnrolled"]))
check("Thêm sinh viên không tồn tại → 400", 400, call("POST", f"/sections/{sec_id}/students", {"studentIds": [99999]}, admin)[0])
check("Thêm danh sách rỗng → 400", 400, call("POST", f"/sections/{sec_id}/students", {"studentIds": []}, admin)[0])
roster = call("GET", f"/sections/{sec_id}/students", token=gv1)[1]
check("GV001 xem danh sách lớp", len(stu_ids), len(roster))
check("GV002 xem danh sách lớp của GV001 → 403", 403, call("GET", f"/sections/{sec_id}/students", token=gv2)[0])
check("StudentCount của lớp cập nhật", len(stu_ids), call("GET", f"/sections/{sec_id}", token=admin)[1]["studentCount"])
check("SV001 đã đăng ký → xem được lớp", 200 if any(x["studentCode"] == "SV001" for x in roster) else 403, call("GET", f"/sections/{sec_id}", token=sv)[0])
check("Xóa lớp còn sinh viên → 409", 409, call("DELETE", f"/sections/{sec_id}", token=admin)[0])
check("Xóa khoa của môn còn dữ liệu → 409", 409, call("DELETE", f"/departments/{cntt}", token=admin)[0])
check("Xóa môn đã mở lớp → 409", 409, call("DELETE", f"/courses/{course_id}", token=admin)[0])
check("Xóa học kỳ đã mở lớp → 409", 409, call("DELETE", f"/semesters/{sem1['id']}", token=admin)[0])
check("PUT /sections giảm số buổi vẫn hợp lệ (chưa điểm danh)", 200, call("PUT", f"/sections/{sec_id}", {**sec_body, "totalSessions": 12}, admin)[0])
check("Unenroll → 204", 204, call("DELETE", f"/sections/{sec_id}/students/{roster[0]['enrollmentId']}", token=admin)[0])
check("Unenroll lại → 404", 404, call("DELETE", f"/sections/{sec_id}/students/{roster[0]['enrollmentId']}", token=admin)[0])

print("== Dọn dữ liệu thử (xóa được những gì xóa được) ==")
for r in call("GET", f"/sections/{sec_id}/students", token=admin)[1]:
    call("DELETE", f"/sections/{sec_id}/students/{r['enrollmentId']}", token=admin)
check("Xóa lớp 1 → 204", 204, call("DELETE", f"/sections/{sec_id}", token=admin)[0])
check("Xóa lớp 2 → 204", 204, call("DELETE", f"/sections/{other_id}", token=admin)[0])
check("Xóa môn → 204", 204, call("DELETE", f"/courses/{course_id}", token=admin)[0])
# Trả học kỳ hiện tại về như cũ rồi xóa 2 học kỳ thử
if old_current:
    o = next(x for x in call("GET", "/semesters", token=admin)[1] if x["id"] == old_current)
    call("PUT", f"/semesters/{old_current}", {"name": o["name"], "startDate": o["startDate"], "endDate": o["endDate"], "isCurrent": True}, admin)
check("Xóa học kỳ A → 204", 204, call("DELETE", f"/semesters/{sem1['id']}", token=admin)[0])
check("Xóa học kỳ B → 204", 204, call("DELETE", f"/semesters/{sem2['id']}", token=admin)[0])
check("Xóa khoa thử → 204", 204, call("DELETE", f"/departments/{dept_id}", token=admin)[0])

print("== Import Excel ==")
def make_xlsx(rows, headers=("MSSV", "Họ tên", "Mã khoa", "Lớp", "Năm nhập học", "Email")):
    wb = openpyxl.Workbook()
    ws = wb.active
    ws.append(list(headers))
    for r in rows:
        ws.append(list(r))
    buf = io.BytesIO()
    wb.save(buf)
    return buf.getvalue()

rows = [
    (f"I{RUN}01", "Nguyễn Import Một", "CNTT", "CNTT-I", 2026, f"i{RUN}01@t.vn"),
    (f"I{RUN}02", "Trần Import Hai", "cntt", "CNTT-I", 2026, ""),          # mã khoa viết thường vẫn nhận
    (f"I{RUN}03", "Lê Sai Khoa", "KHONGCO", "CNTT-I", 2026, ""),           # lỗi: khoa
    ("SV001", "Trùng MSSV", "CNTT", "CNTT-I", 2026, ""),                    # lỗi: trùng MSSV có sẵn
    (f"I{RUN}05", "Phạm Sai Năm", "CNTT", "CNTT-I", "abc", ""),           # lỗi: năm
    (f"I{RUN}01", "Trùng Trong File", "CNTT", "CNTT-I", 2026, ""),         # lỗi: trùng dòng 1 trong file
    (None, None, None, None, None, None),                                   # dòng trống bị bỏ qua
]
body, ctype = multipart("file", "sv.xlsx", make_xlsx(rows))
s, res, _ = call("POST", "/import/students", token=admin, raw=body, content_type=ctype)
check("POST /import/students → 200", 200, s)
check("Import: tổng 6 dòng có dữ liệu", 6, res["totalRows"])
check("Import: tạo 2 sinh viên", 2, res["created"])
check("Import: bỏ qua 4 dòng lỗi", 4, res["skipped"])
print("       Lỗi báo về:", [(e["row"], e["message"]) for e in res["errors"]])
check("Sinh viên import đăng nhập được bằng IMPORT_DEFAULT_PASSWORD", True, login(f"I{RUN}01", ENV["IMPORT_DEFAULT_PASSWORD"]) is not None)
tok_i = login(f"I{RUN}02", ENV["IMPORT_DEFAULT_PASSWORD"])
check("Role của sinh viên import = Student", "Student", call("GET", "/auth/me", token=tok_i)[1]["role"])
check("Import lần 2 cùng file → tạo 0 (đã tồn tại hết)", 0, call("POST", "/import/students", token=admin, raw=body, content_type=ctype)[1]["created"])
b2, c2 = multipart("file", "sv.txt", b"abc", "text/plain")
check("Import sai đuôi file → 400", 400, call("POST", "/import/students", token=admin, raw=b2, content_type=c2)[0])
b3, c3 = multipart("file", "bad.xlsx", b"day khong phai excel")
check("Import file hỏng → 400", 400, call("POST", "/import/students", token=admin, raw=b3, content_type=c3)[0])
b4, c4 = multipart("file", "thieu.xlsx", make_xlsx([("A", "B")], headers=("MSSV", "Họ tên")))
check("Import thiếu cột → 400", 400, call("POST", "/import/students", token=admin, raw=b4, content_type=c4)[0])
check("Import bằng Lecturer → 403", 403, call("POST", "/import/students", token=gv1, raw=body, content_type=ctype)[0])

print("== Đổi mật khẩu ==")
check("Đổi mật khẩu sai mật khẩu hiện tại → 400", 400, call("POST", "/auth/change-password", {"currentPassword": "sai", "newPassword": "MatKhauMoi12345"}, tok_i)[0])
check("Đổi mật khẩu quá ngắn → 400", 400, call("POST", "/auth/change-password", {"currentPassword": ENV["IMPORT_DEFAULT_PASSWORD"], "newPassword": "123"}, tok_i)[0])
check("Đổi mật khẩu đúng → 204", 204, call("POST", "/auth/change-password", {"currentPassword": ENV["IMPORT_DEFAULT_PASSWORD"], "newPassword": "MatKhauMoi12345"}, tok_i)[0])
check("Đăng nhập bằng mật khẩu mới", True, login(f"I{RUN}02", "MatKhauMoi12345") is not None)
check("Đăng nhập bằng mật khẩu cũ bị từ chối", None, login(f"I{RUN}02", ENV["IMPORT_DEFAULT_PASSWORD"]))
check("Không token đổi mật khẩu → 401", 401, call("POST", "/auth/change-password", {"currentPassword": "a", "newPassword": "b" * 8})[0])

sys.exit(0 if summary() else 1)
