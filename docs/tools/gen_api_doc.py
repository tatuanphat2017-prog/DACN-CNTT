"""Sinh docs/API.md (bảng tóm tắt API) từ docs/api/openapi.json.

Cách dùng (chạy từ thư mục gốc repo):
    python -X utf8 docs/tools/gen_api_doc.py

Cập nhật openapi.json: chạy backend rồi tải http://localhost:8080/swagger/v1/swagger.json về docs/api/openapi.json.
"""
import json
import re
from collections import defaultdict

SPEC = "docs/api/openapi.json"
OUT = "docs/API.md"
METHOD_ORDER = {"get": 0, "post": 1, "put": 2, "delete": 3}

# Tên nhóm hiển thị theo thứ tự (khóa = tag Swagger = tên controller)
GROUPS = [
    ("Auth", "Đăng nhập"),
    ("Users", "Quản trị: tài khoản"),
    ("People", "Quản trị: danh sách sinh viên, giảng viên"),
    ("Departments", "Danh mục: khoa"),
    ("Courses", "Danh mục: môn học"),
    ("Semesters", "Danh mục: học kỳ"),
    ("Sections", "Lớp học phần"),
    ("Import", "Nhập Excel"),
    ("Attendance", "Điểm danh"),
    ("Grades", "Điểm"),
    ("Risk", "Rủi ro (Machine Learning)"),
    ("Warnings", "Cảnh báo"),
    ("StudentPortal", "Cổng sinh viên"),
    ("Ml", "Quản lý mô hình ML"),
    ("Health", "Kiểm tra hệ thống"),
]


def access(description: str) -> str:
    m = re.search(r"\*\*Quyền truy cập:\*\* (.+)", description or "")
    text = m.group(1).strip() if m else "?"
    return (text.replace("Role ", "").replace("Công khai, không cần đăng nhập", "Công khai")
            .replace("Mọi người dùng đã đăng nhập", "Mọi role"))


def main() -> None:
    spec = json.load(open(SPEC, encoding="utf-8"))
    by_tag = defaultdict(list)
    for path, item in spec["paths"].items():
        for method, op in item.items():
            by_tag[op["tags"][0]].append((method, path, op))

    total = sum(len(v) for v in by_tag.values())
    mock = sum(1 for v in by_tag.values() for _, _, op in v if "**MOCK:**" in (op.get("description") or ""))

    lines = [
        "# Tài liệu API",
        "",
        "> File này sinh tự động từ `docs/api/openapi.json` bằng `docs/tools/gen_api_doc.py`. "
        "Chi tiết dữ liệu gửi/nhận xem trên Swagger (`/swagger`) của backend.",
        "",
        f"**{total} endpoint**, trong đó **{mock} endpoint đang MOCK** (trả dữ liệu mẫu cố định, chưa có logic thật; "
        "response có header `X-Mock-Data: true`). Endpoint không MOCK là đã chạy thật.",
        "",
        "## Quy ước chung",
        "",
        "- **Đăng nhập:** `POST /api/auth/login` → lấy `accessToken`, gửi kèm mọi request cần đăng nhập: `Authorization: Bearer <accessToken>`.",
        "- **Role:** `Admin`, `Lecturer`, `Student` (chữ, không phải số). Sai role trả **403**, chưa đăng nhập hoặc token hết hạn trả **401**.",
        "- **Enum dạng chữ:** `RiskLevel` = `Low` | `Medium` | `High`; `AttendanceStatus` = `Present` | `Absent` | `Late` | `Excused`.",
        "- **Phân trang:** `?page=1&pageSize=20`, kết quả `{ items, page, pageSize, totalItems }`.",
        "- **Ngày:** `DateOnly` dạng `yyyy-MM-dd`; thời gian là UTC dạng ISO 8601.",
        "- **Lỗi:** 400 (dữ liệu sai, kèm chi tiết từng trường), 404 (không tìm thấy), 409 (trùng hoặc còn dữ liệu liên quan), 429 (đăng nhập sai quá nhiều), 503 (ML service không khả dụng).",
        "- **Mức rủi ro:** `Low` xác suất rớt < 0.3, `Medium` 0.3–0.6, `High` > 0.6.",
        "",
    ]
    for tag, title in GROUPS:
        ops = sorted(by_tag.get(tag, []), key=lambda x: (x[1], METHOD_ORDER.get(x[0], 9)))
        if not ops:
            continue
        lines += [f"## {title}", "", "| Method | Đường dẫn | Quyền | Mô tả | Mock |", "|---|---|---|---|---|"]
        for method, path, op in ops:
            summary = (op.get("summary") or "").replace("|", "\\|").replace("\n", " ")
            is_mock = "✅" if "**MOCK:**" in (op.get("description") or "") else ""
            lines.append(f"| `{method.upper()}` | `{path}` | {access(op.get('description'))} | {summary} | {is_mock} |")
        lines.append("")

    open(OUT, "w", encoding="utf-8").write("\n".join(lines))
    print(f"Đã tạo {OUT}: {total} endpoint, {mock} mock")


if __name__ == "__main__":
    main()
