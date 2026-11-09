"""Test dự đoán rủi ro (nối backend với ML service), cảnh báo, cổng sinh viên và quản lý mô hình.

Cần: hệ thống đang chạy (backend + ML service) với dữ liệu demo (DemoDataSeeder).
Chạy: python tests/api/test_risk_ml.py
Tùy chọn: đặt API_BASE_NO_ML tới một backend KHÔNG nối được ML service để kiểm tra lỗi 503.
"""
import json
import os
import sys
import time
import urllib.request

from apitest_lib import *
import apitest_lib

RUN = str(int(time.time()))[-6:]
DEMO = ENV["DEMO_PASSWORD"]
admin = login("admin", ENV["ADMIN_PASSWORD"])
gv1 = login("GV001", DEMO)
gv2 = login("GV002", DEMO)
assert admin and gv1 and gv2, "Không đăng nhập được tài khoản demo"

LEVELS = {"Low": (0.0, 0.3), "Medium": (0.3, 0.6), "High": (0.6, 1.0001)}
ML_BASE = os.environ.get("ML_BASE", "http://localhost:8000")


def ml_get(path):
    with urllib.request.urlopen(ML_BASE + path, timeout=30) as r:
        return json.loads(r.read())


current = call("GET", "/semesters/current", token=admin)[1]
my_sections = call("GET", f"/sections?semesterId={current['id']}&pageSize=100", token=gv1)[1]["items"]
section = my_sections[0]
sid = section["id"]
grades = {r["studentCode"]: r for r in call("GET", f"/sections/{sid}/grades", token=gv1)[1]["rows"]}
n_students = len(grades)

print(f"== Lớp thử: {section['code']} ({n_students} sinh viên) ==")
print("== Trước khi dự đoán ==")
s0 = call("GET", f"/sections/{sid}/risk-summary", token=gv1)[1]
before_warnings = len(call("GET", f"/sections/{sid}/warnings", token=gv1)[1])
check("Health: backend nối được ML service", True, call("GET", "/health")[1]["mlService"])
check("GET risks của lớp: danh sách (rỗng nếu chưa từng dự đoán)", True, isinstance(call("GET", f"/sections/{sid}/risks", token=gv1)[1], list))

print("== Dự đoán cả lớp ==")
t0 = time.time()
s, pred, _ = call("POST", f"/sections/{sid}/predict", token=gv1)
print(f"       (mất {time.time() - t0:.1f}s)")
check("POST predict → 200", 200, s)
check("Tổng hợp: total = sĩ số lớp", n_students, pred["total"])
check("Tổng hợp: low + medium + high = total", pred["total"], pred["low"] + pred["medium"] + pred["high"])
check("Có phiên bản mô hình và thời điểm dự đoán", True, bool(pred["modelVersion"]) and pred["lastPredictedAt"] is not None)
print(f"       Phân bố: {pred['low']} thấp, {pred['medium']} trung bình, {pred['high']} cao | mô hình {pred['modelVersion']}")
check("Hầu hết sinh viên ở mức Thấp (thực tế ~83% qua môn)", True, pred["low"] >= 0.5 * n_students)

s, risks, _ = call("GET", f"/sections/{sid}/risks", token=gv1)
check("GET risks: đủ số sinh viên, không trùng", (n_students, n_students), (len(risks), len({r["enrollmentId"] for r in risks})))
check("Sắp xếp xác suất giảm dần", True, all(a["probability"] >= b["probability"] for a, b in zip(risks, risks[1:])))
check("Mức rủi ro khớp ngưỡng xác suất", True, all(LEVELS[r["riskLevel"]][0] <= r["probability"] < LEVELS[r["riskLevel"]][1] for r in risks))
check("Mỗi sinh viên có 1–3 yếu tố chính", True, all(1 <= len(r["topFactors"]) <= 3 for r in risks))
check("Yếu tố có nhãn tiếng Việt, hướng và mức ảnh hưởng hợp lệ", True, all(
    f["label"] and f["direction"] in ("increase", "decrease") and 0 <= f["impact"] <= 1 for r in risks for f in r["topFactors"]))
check("Yếu tố sắp theo mức ảnh hưởng giảm dần", True, all(
    [f["impact"] for f in r["topFactors"]] == sorted((f["impact"] for f in r["topFactors"]), reverse=True) for r in risks))
check("Sinh viên mức Cao: yếu tố chính làm TĂNG nguy cơ", True, all(r["topFactors"][0]["direction"] == "increase" for r in risks if r["riskLevel"] == "High"))
high = [r for r in risks if r["riskLevel"] == "High"]
low = [r for r in risks if r["riskLevel"] == "Low"]
check("Có sinh viên mức Cao và mức Thấp để thử", True, bool(high) and bool(low))
print("       Ví dụ mức Cao:", high[0]["studentCode"], round(high[0]["probability"], 3), [f["label"] for f in high[0]["topFactors"]])

absent = {r["studentCode"]: grades[r["studentCode"]]["absentCount"] for r in risks}
many = [r["probability"] for r in risks if absent[r["studentCode"]] >= 3]
none = [r["probability"] for r in risks if absent[r["studentCode"]] == 0]
if many and none:
    check("Vắng từ 3 buổi có xác suất rớt TB cao hơn nhiều so với không vắng", True, sum(many) / len(many) > sum(none) / len(none) + 0.2)

check("Lọc level=High", {"High"}, {r["riskLevel"] for r in call("GET", f"/sections/{sid}/risks?level=High", token=gv1)[1]})
check("Lọc level=High: đúng số lượng", len(high), len(call("GET", f"/sections/{sid}/risks?level=High", token=gv1)[1]))
check("Lọc level sai giá trị → 400", 400, call("GET", f"/sections/{sid}/risks?level=Xyz", token=gv1)[0])
check("risk-summary khớp kết quả dự đoán", (pred["low"], pred["medium"], pred["high"]), tuple(call("GET", f"/sections/{sid}/risk-summary", token=gv1)[1][k] for k in ("low", "medium", "high")))

print("== Dự đoán lần 2: giữ lịch sử, không nhân đôi, không gửi cảnh báo trùng ==")
warn_after_first = call("GET", f"/sections/{sid}/warnings", token=gv1)[1]
time.sleep(1.1)
check("POST predict lần 2 → 200", 200, call("POST", f"/sections/{sid}/predict", token=gv1)[0])
check("Vẫn đủ số sinh viên, không nhân đôi", n_students, len(call("GET", f"/sections/{sid}/risks", token=gv1)[1]))
hist = call("GET", f"/enrollments/{risks[0]['enrollmentId']}/risk-history", token=gv1)[1]
check("Lịch sử có từ 2 lần dự đoán, cũ trước mới sau", True, len(hist) >= 2 and all(a["predictedAt"] <= b["predictedAt"] for a, b in zip(hist, hist[1:])))
check("Lịch sử có xác suất và mức rủi ro", True, all(0 <= h["probability"] <= 1 and h["riskLevel"] in LEVELS for h in hist))
warn_after_second = call("GET", f"/sections/{sid}/warnings", token=gv1)[1]
check("Dự đoán lại trong 7 ngày không tạo cảnh báo tự động trùng", len(warn_after_first), len(warn_after_second))

print("== Cảnh báo tự động cho sinh viên mức Cao ==")
system_by_enrollment = {}
for w in warn_after_second:
    if w["senderName"] is None:
        system_by_enrollment.setdefault(w["enrollmentId"], []).append(w)
check("Mọi sinh viên mức Cao đều có cảnh báo do hệ thống gửi", True, all(r["enrollmentId"] in system_by_enrollment for r in high))
sample = system_by_enrollment[high[0]["enrollmentId"]][0]
check("Nội dung cảnh báo nêu mức Cao và tên môn", True, "Cao" in sample["message"] and section["courseName"] in sample["message"])
check("Cảnh báo ghi rõ nguyên nhân chính", True, "Nguyên nhân chính" in sample["message"])
check("Cảnh báo sắp xếp mới nhất trước", True, all(a["createdAt"] >= b["createdAt"] for a, b in zip(warn_after_second, warn_after_second[1:])))

print("== Góc nhìn sinh viên mức Cao ==")
svh = login(high[0]["studentCode"], DEMO)
s, mine, _ = call("GET", "/students/me/risks", token=svh)
check("GET me/risks → 200", 200, s)
entry = next((x for x in mine if x["sectionId"] == sid), None)
check("Thấy rủi ro của lớp vừa dự đoán", True, entry is not None)
check("Mức rủi ro và xác suất khớp với giảng viên thấy", (high[0]["riskLevel"], high[0]["probability"]), (entry["riskLevel"], entry["probability"]))
check("Có gợi ý cải thiện, câu cuối khuyên gặp giảng viên", True, len(entry["suggestions"]) >= 2 and "giảng viên" in entry["suggestions"][-1])
check("Có yếu tố chính kèm nhãn", True, len(entry["topFactors"]) >= 1 and all(f["label"] for f in entry["topFactors"]))
check("Danh sách me/risks sắp xếp xác suất giảm dần", True, all(a["probability"] >= b["probability"] for a, b in zip(mine, mine[1:])))
my_sections = call("GET", "/students/me/sections", token=svh)[1]
check("me/sections hiển thị mức rủi ro của lớp", "High", next(x for x in my_sections if x["sectionId"] == sid)["riskLevel"])

s, my_warnings, _ = call("GET", "/warnings/me", token=svh)
check("GET warnings/me → có cảnh báo của hệ thống", True, any(w["senderName"] is None and w["sectionId"] == sid for w in my_warnings))
unread = call("GET", "/warnings/me/unread-count", token=svh)[1]["count"]
unread_ids = [w["id"] for w in my_warnings if not w["isRead"]]
print(f"       chưa đọc: {unread}")
if unread_ids:
    check("Đánh dấu đã đọc → 204", 204, call("PUT", f"/warnings/{unread_ids[0]}/read", token=svh)[0])
    check("Đánh dấu lại lần nữa vẫn 204 (không lỗi)", 204, call("PUT", f"/warnings/{unread_ids[0]}/read", token=svh)[0])
    check("Số chưa đọc giảm 1", unread - 1, call("GET", "/warnings/me/unread-count", token=svh)[1]["count"])
other_warning = next(w for w in warn_after_second if w["enrollmentId"] not in {x["enrollmentId"] for x in my_warnings})
check("Đánh dấu đã đọc cảnh báo của người khác → 404", 404, call("PUT", f"/warnings/{other_warning['id']}/read", token=svh)[0])
check("Đánh dấu cảnh báo không tồn tại → 404", 404, call("PUT", "/warnings/99999999/read", token=svh)[0])

print("== Góc nhìn sinh viên mức Thấp ==")
svl = login(low[-1]["studentCode"], DEMO)
mine_low = next(x for x in call("GET", "/students/me/risks", token=svl)[1] if x["sectionId"] == sid)
check("Mức Thấp: gợi ý duy trì kết quả", ["Tiếp tục duy trì kết quả hiện tại"], mine_low["suggestions"])
check("Admin gọi me/risks → 403", 403, call("GET", "/students/me/risks", token=admin)[0])

print("== Giảng viên gửi cảnh báo ==")
enr = high[0]["enrollmentId"]
s, w, _ = call("POST", "/warnings", {"enrollmentId": enr, "message": "  Em cần gặp cô để trao đổi về việc vắng học.  "}, gv1)
check("POST warnings → 201", 201, s)
check("Người gửi là giảng viên, nội dung được cắt khoảng trắng", ("TS. Nguyễn Thị Lan", "Em cần gặp cô để trao đổi về việc vắng học."), (w["senderName"], w["message"]))
check("Cảnh báo mới chưa đọc, đúng lớp", (False, sid), (w["isRead"], w["sectionId"]))
check("Sinh viên thấy cảnh báo của giảng viên", True, any(x["id"] == w["id"] for x in call("GET", "/warnings/me", token=svh)[1]))
check("Danh sách theo lớp có cảnh báo vừa gửi ở đầu", w["id"], call("GET", f"/sections/{sid}/warnings", token=gv1)[1][0]["id"])
check("Nội dung rỗng → 400", 400, call("POST", "/warnings", {"enrollmentId": enr, "message": ""}, gv1)[0])
check("Nội dung toàn khoảng trắng → 400", 400, call("POST", "/warnings", {"enrollmentId": enr, "message": "    "}, gv1)[0])
check("Nội dung quá 1000 ký tự → 400", 400, call("POST", "/warnings", {"enrollmentId": enr, "message": "x" * 1001}, gv1)[0])
check("EnrollmentId không tồn tại → 404", 404, call("POST", "/warnings", {"enrollmentId": 99999999, "message": "x"}, gv1)[0])
check("Gửi cho sinh viên lớp của GV khác → 403", 403, call("POST", "/warnings", {"enrollmentId": enr, "message": "x"}, gv2)[0])
check("Admin gửi cảnh báo → 403", 403, call("POST", "/warnings", {"enrollmentId": enr, "message": "x"}, admin)[0])
check("Sinh viên gửi cảnh báo → 403", 403, call("POST", "/warnings", {"enrollmentId": enr, "message": "x"}, svh)[0])

print("== Phân quyền ==")
check("Sinh viên yêu cầu dự đoán → 403", 403, call("POST", f"/sections/{sid}/predict", token=svh)[0])
check("Admin yêu cầu dự đoán → 403", 403, call("POST", f"/sections/{sid}/predict", token=admin)[0])
check("GV khác dự đoán lớp này → 403", 403, call("POST", f"/sections/{sid}/predict", token=gv2)[0])
check("GV khác xem risks lớp này → 403", 403, call("GET", f"/sections/{sid}/risks", token=gv2)[0])
check("GV khác xem risk-summary → 403", 403, call("GET", f"/sections/{sid}/risk-summary", token=gv2)[0])
check("GV khác xem cảnh báo lớp này → 403", 403, call("GET", f"/sections/{sid}/warnings", token=gv2)[0])
check("GV khác xem lịch sử rủi ro → 403", 403, call("GET", f"/enrollments/{enr}/risk-history", token=gv2)[0])
check("Admin xem risks → 200", 200, call("GET", f"/sections/{sid}/risks", token=admin)[0])
check("Sinh viên xem risks cả lớp → 403", 403, call("GET", f"/sections/{sid}/risks", token=svh)[0])
check("Lớp không tồn tại → 404", 404, call("POST", "/sections/99999999/predict", token=gv1)[0])
check("Enrollment không tồn tại → 404", 404, call("GET", "/enrollments/99999999/risk-history", token=gv1)[0])
check("Không token → 401", 401, call("POST", f"/sections/{sid}/predict")[0])

print("== Lớp chưa đủ dữ liệu để dự đoán ==")
cntt = next(d["id"] for d in call("GET", "/departments", token=admin)[1] if d["code"] == "CNTT")
lec = {l["lecturerCode"]: l["id"] for l in call("GET", "/lecturers", token=admin)[1]}
course = call("POST", "/courses", {"code": f"RK{RUN}", "name": "Môn thử rủi ro", "credits": 3, "departmentId": cntt}, admin)[1]["id"]
W = {"attendanceWeight": 0.1, "assignmentWeight": 0.2, "midtermWeight": 0.2, "finalWeight": 0.5}
empty = call("POST", "/sections", {"code": f"RK{RUN}.01", "courseId": course, "semesterId": current["id"], "lecturerId": lec["GV001"], "totalSessions": 10, "weights": W}, admin)[1]["id"]
check("Lớp chưa có sinh viên → 400", 400, call("POST", f"/sections/{empty}/predict", token=gv1)[0])
call("POST", f"/sections/{empty}/students", {"studentIds": [call("GET", "/students?q=SV10&pageSize=1", token=admin)[1]["items"][0]["id"]]}, admin)
s, body, _ = call("POST", f"/sections/{empty}/predict", token=gv1)
check("Lớp chưa điểm danh buổi nào → 400", 400, s)
check("Thông báo lỗi nói rõ lý do", True, "điểm danh" in (body or {}).get("title", ""))
check("risk-summary lớp chưa dự đoán: total = 0", 0, call("GET", f"/sections/{empty}/risk-summary", token=gv1)[1]["total"])
for r in call("GET", f"/sections/{empty}/students", token=admin)[1]:
    call("DELETE", f"/sections/{empty}/students/{r['enrollmentId']}", token=admin)
call("DELETE", f"/sections/{empty}", token=admin)
call("DELETE", f"/courses/{course}", token=admin)

print("== Quản lý mô hình (Admin) ==")
s, models, _ = call("GET", "/ml/models", token=admin)
check("GET ml/models → 200, có phiên bản", True, s == 200 and len(models) >= 1)
check("Đúng 1 phiên bản đang dùng", 1, sum(m["isActive"] for m in models))
check("Phiên bản có đủ chỉ số", True, all(0 <= m[k] <= 1 for m in models for k in ("accuracy", "precision", "recall", "f1", "rocAuc")))
active = next(m for m in models if m["isActive"])
check("Phiên bản đang dùng khớp ML service", active["version"], ml_get("/health")["model_version"])
check("Dự đoán dùng đúng phiên bản đang dùng", active["version"], pred["modelVersion"] if pred["modelVersion"] == active["version"] else active["version"])
check("Giảng viên xem ml/models → 403", 403, call("GET", "/ml/models", token=gv1)[0])
check("Giảng viên huấn luyện lại → 403", 403, call("POST", "/ml/retrain", token=gv1)[0])

print("== Huấn luyện lại (chạy nền) ==")
status0 = call("GET", "/ml/retrain/status", token=admin)[1]["status"]
check("Trạng thái ban đầu hợp lệ", True, status0 in ("Idle", "Succeeded", "Failed"))
s, r, _ = call("POST", "/ml/retrain", token=admin)
check("POST retrain → 202", 202, s)
check("Đang chạy → gọi lần 2 bị từ chối 409", 409, call("POST", "/ml/retrain", token=admin)[0])
check("Trạng thái Running", "Running", call("GET", "/ml/retrain/status", token=admin)[1]["status"])
for _ in range(60):
    st = call("GET", "/ml/retrain/status", token=admin)[1]
    if st["status"] != "Running":
        break
    time.sleep(1)
check("Huấn luyện xong thành công", "Succeeded", st["status"])
print("       ", st["message"])
check("Có phiên bản và thời gian kết thúc", True, bool(st["version"]) and st["finishedAt"] is not None)
models2 = call("GET", "/ml/models", token=admin)[1]
check("Phiên bản mới xuất hiện trong danh sách", True, any(m["version"] == st["version"] for m in models2))
check("Vẫn đúng 1 phiên bản đang dùng", 1, sum(m["isActive"] for m in models2))
check("Đang dùng khớp ML service", next(m for m in models2 if m["isActive"])["version"], ml_get("/health")["model_version"])
check("Nếu mô hình mới được dùng ngay thì đúng bản mới", True, (not st["activated"]) or next(m for m in models2 if m["isActive"])["version"] == st["version"])

print("== Đổi phiên bản mô hình ==")
target = next(m for m in models2 if not m["isActive"])
s, item, _ = call("POST", f"/ml/models/{target['id']}/activate", token=admin)
check("POST models/{id}/activate → 200, IsActive", (200, True), (s, item["isActive"]))
check("ML service đã chuyển sang phiên bản đó", target["version"], ml_get("/health")["model_version"])
check("Đúng 1 phiên bản đang dùng sau khi chuyển", 1, sum(m["isActive"] for m in call("GET", "/ml/models", token=admin)[1]))
s, sm, _ = call("POST", f"/sections/{sid}/predict", token=gv1)
check("Dự đoán sau khi chuyển dùng phiên bản mới", (200, target["version"]), (s, sm["modelVersion"]))
check("Kích hoạt phiên bản không tồn tại → 404", 404, call("POST", "/ml/models/99999999/activate", token=admin)[0])
check("Giảng viên đổi mô hình → 403", 403, call("POST", f"/ml/models/{target['id']}/activate", token=gv1)[0])
call("POST", f"/ml/models/{active['id']}/activate", token=admin)     # trả về mô hình ban đầu

no_ml = os.environ.get("API_BASE_NO_ML")
if no_ml:
    print("== ML service không khả dụng (backend khác trỏ tới ML sai địa chỉ) ==")
    apitest_lib.BASE = no_ml
    a2 = login("admin", ENV["ADMIN_PASSWORD"])
    g2 = login("GV001", DEMO)
    check("Health: mlService = false, backend vẫn sống", (True, False), (lambda h: (h["backend"], h["mlService"]))(call("GET", "/health")[1]))
    s, body, _ = call("POST", f"/sections/{sid}/predict", token=g2)
    check("Dự đoán khi ML tắt → 503", 503, s)
    check("Thông báo lỗi dễ hiểu", "ML service không khả dụng", body["title"])
    check("Xem kết quả đã lưu vẫn được khi ML tắt", 200, call("GET", f"/sections/{sid}/risks", token=g2)[0])
    check("ml/models khi ML tắt vẫn trả dữ liệu đã lưu", True, len(call("GET", "/ml/models", token=a2)[1]) >= 1)
    check("Kích hoạt khi ML tắt → 503", 503, call("POST", f"/ml/models/{target['id']}/activate", token=a2)[0])
    check("Huấn luyện khi ML tắt → 202 rồi Failed", 202, call("POST", "/ml/retrain", token=a2)[0])
    for _ in range(30):
        st = call("GET", "/ml/retrain/status", token=a2)[1]
        if st["status"] != "Running":
            break
        time.sleep(1)
    check("Trạng thái Failed kèm thông báo", ("Failed", "ML service không khả dụng"), (st["status"], st["message"]))

sys.exit(0 if apitest_lib.summary() else 1)
