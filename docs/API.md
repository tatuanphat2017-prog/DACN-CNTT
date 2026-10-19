# Tài liệu API

> File này sinh tự động từ `docs/api/openapi.json` bằng `docs/tools/gen_api_doc.py`. Chi tiết dữ liệu gửi/nhận xem trên Swagger (`/swagger`) của backend.

**51 endpoint**, trong đó **48 endpoint đang MOCK** (trả dữ liệu mẫu cố định, chưa có logic thật; response có header `X-Mock-Data: true`). Endpoint không MOCK là đã chạy thật.

## Quy ước chung

- **Đăng nhập:** `POST /api/auth/login` → lấy `accessToken`, gửi kèm mọi request cần đăng nhập: `Authorization: Bearer <accessToken>`.
- **Role:** `Admin`, `Lecturer`, `Student` (chữ, không phải số). Sai role trả **403**, chưa đăng nhập hoặc token hết hạn trả **401**.
- **Enum dạng chữ:** `RiskLevel` = `Low` | `Medium` | `High`; `AttendanceStatus` = `Present` | `Absent` | `Late` | `Excused`.
- **Phân trang:** `?page=1&pageSize=20`, kết quả `{ items, page, pageSize, totalItems }`.
- **Ngày:** `DateOnly` dạng `yyyy-MM-dd`; thời gian là UTC dạng ISO 8601.
- **Lỗi:** 400 (dữ liệu sai, kèm chi tiết từng trường), 404 (không tìm thấy), 409 (trùng hoặc còn dữ liệu liên quan), 429 (đăng nhập sai quá nhiều), 503 (ML service không khả dụng).
- **Mức rủi ro:** `Low` xác suất rớt < 0.3, `Medium` 0.3–0.6, `High` > 0.6.

## Đăng nhập

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `POST` | `/api/auth/login` | Công khai | Đăng nhập bằng username (MSSV / mã GV / admin) và mật khẩu, trả về JWT. |  |
| `GET` | `/api/auth/me` | Mọi role | Thông tin người đang đăng nhập (cần gửi kèm token). |  |

## Quản trị: tài khoản

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `GET` | `/api/users` | Admin | Danh sách người dùng, lọc theo role và từ khóa (username hoặc họ tên), có phân trang. | ✅ |
| `POST` | `/api/users` | Admin | Tạo tài khoản. Role = Student phải có Student, Role = Lecturer phải có Lecturer. Username trùng trả 409. | ✅ |
| `GET` | `/api/users/{id}` | Admin | Chi tiết một người dùng, kèm hồ sơ sinh viên hoặc giảng viên. | ✅ |
| `PUT` | `/api/users/{id}` | Admin | Sửa thông tin. Muốn khóa tài khoản thì đặt IsActive = false (không xóa dòng dữ liệu để giữ lịch sử). | ✅ |
| `POST` | `/api/users/{id}/reset-password` | Admin | Đặt lại mật khẩu cho người dùng. | ✅ |

## Quản trị: danh sách sinh viên, giảng viên

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `GET` | `/api/lecturers` | Admin | Danh sách giảng viên. | ✅ |
| `GET` | `/api/students` | Admin | Danh sách sinh viên, tìm theo mã hoặc họ tên, có phân trang. | ✅ |

## Danh mục: khoa

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `GET` | `/api/departments` | Mọi role | Danh sách khoa. | ✅ |
| `POST` | `/api/departments` | Admin | Thêm khoa. Mã khoa trùng trả 409. | ✅ |
| `PUT` | `/api/departments/{id}` | Admin | Sửa khoa. | ✅ |
| `DELETE` | `/api/departments/{id}` | Admin | Xóa khoa. Còn môn học, sinh viên hoặc giảng viên thuộc khoa thì trả 409. | ✅ |

## Danh mục: môn học

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `GET` | `/api/courses` | Mọi role | Danh sách môn học, lọc theo khoa và từ khóa (mã hoặc tên), có phân trang. | ✅ |
| `POST` | `/api/courses` | Admin | Thêm môn học. Mã môn trùng trả 409. | ✅ |
| `PUT` | `/api/courses/{id}` | Admin | Sửa môn học. | ✅ |
| `DELETE` | `/api/courses/{id}` | Admin | Xóa môn học. Môn đã mở lớp thì trả 409. | ✅ |

## Danh mục: học kỳ

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `GET` | `/api/semesters` | Mọi role | Danh sách học kỳ. | ✅ |
| `POST` | `/api/semesters` | Admin | Thêm học kỳ. Ngày kết thúc phải sau ngày bắt đầu. | ✅ |
| `GET` | `/api/semesters/current` | Mọi role | Học kỳ hiện tại (IsCurrent = true). | ✅ |
| `PUT` | `/api/semesters/{id}` | Admin | Sửa học kỳ. | ✅ |
| `DELETE` | `/api/semesters/{id}` | Admin | Xóa học kỳ. Học kỳ đã mở lớp thì trả 409. | ✅ |

## Lớp học phần

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `GET` | `/api/sections` | Mọi role | Danh sách lớp học phần, lọc theo học kỳ, môn học, có phân trang. | ✅ |
| `POST` | `/api/sections` | Admin | Mở lớp học phần. Tổng 4 trọng số phải bằng 1. Mã lớp trùng trả 409. | ✅ |
| `GET` | `/api/sections/{id}` | Mọi role | Chi tiết một lớp học phần, kèm trọng số điểm. | ✅ |
| `PUT` | `/api/sections/{id}` | Admin | Sửa lớp học phần. | ✅ |
| `DELETE` | `/api/sections/{id}` | Admin | Xóa lớp học phần. Lớp đã có sinh viên đăng ký hoặc điểm danh thì trả 409. | ✅ |
| `GET` | `/api/sections/{id}/students` | Admin hoặc Lecturer | Danh sách sinh viên trong lớp (kèm EnrollmentId, dùng cho điểm danh, điểm và cảnh báo). | ✅ |
| `POST` | `/api/sections/{id}/students` | Admin | Thêm sinh viên vào lớp. Sinh viên đã có trong lớp được bỏ qua, không báo lỗi. | ✅ |
| `DELETE` | `/api/sections/{id}/students/{enrollmentId}` | Admin | Xóa sinh viên khỏi lớp (xóa luôn điểm danh, điểm, dự đoán rủi ro, cảnh báo của sinh viên trong lớp). | ✅ |

## Nhập Excel

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `POST` | `/api/import/students` | Admin | Nhập danh sách sinh viên từ file .xlsx (gửi dạng multipart/form-data, field tên "file"). Cột bắt buộc: MSSV, Họ tên, Mã khoa, Lớp, Năm nhập học. Dòng lỗi được báo trong Errors, các dòng đúng vẫn được tạo. | ✅ |

## Điểm danh

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `GET` | `/api/sections/{sectionId}/attendance` | Admin hoặc Lecturer | Tóm tắt tất cả các buổi của lớp (đủ TotalSessions buổi, buổi chưa diễn ra có Date = null). | ✅ |
| `GET` | `/api/sections/{sectionId}/attendance/{sessionNo}` | Admin hoặc Lecturer | Bảng điểm danh của một buổi: mỗi sinh viên một trạng thái (Present, Absent, Late, Excused). | ✅ |
| `PUT` | `/api/sections/{sectionId}/attendance/{sessionNo}` | Lecturer | Lưu điểm danh một buổi (tạo mới hoặc ghi đè). Backend tự tính lại AttendanceScore, TotalScore, IsPassed của lớp. Trả 400 nếu có EnrollmentId không thuộc lớp. | ✅ |

## Điểm

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `GET` | `/api/sections/{sectionId}/grades` | Admin hoặc Lecturer | Bảng điểm cả lớp. AttendanceScore, TotalScore, IsPassed do backend tính, chỉ đọc. | ✅ |
| `PUT` | `/api/sections/{sectionId}/grades` | Lecturer | Lưu điểm bài tập, giữa kỳ, cuối kỳ cho nhiều sinh viên cùng lúc. Điểm phải trong khoảng 0–10 (sai trả 400). Có FinalScore thì backend tự tính TotalScore và IsPassed. Trả về bảng điểm đã cập nhật. | ✅ |

## Rủi ro (Machine Learning)

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `GET` | `/api/enrollments/{enrollmentId}/risk-history` | Admin hoặc Lecturer | Lịch sử dự đoán của một sinh viên trong lớp (theo EnrollmentId), dùng vẽ biểu đồ xu hướng. | ✅ |
| `POST` | `/api/sections/{sectionId}/predict` | Lecturer | Chạy dự đoán cho cả lớp (gọi ML service, lưu kết quả). Sinh viên ra mức High sẽ được hệ thống tự gửi cảnh báo. Trả về tổng hợp số sinh viên theo từng mức rủi ro. Trả 503 nếu ML service không khả dụng. | ✅ |
| `GET` | `/api/sections/{sectionId}/risk-summary` | Admin hoặc Lecturer | Tổng hợp số sinh viên theo mức rủi ro của lớp (dùng vẽ biểu đồ dashboard). | ✅ |
| `GET` | `/api/sections/{sectionId}/risks` | Admin hoặc Lecturer | Kết quả dự đoán mới nhất của từng sinh viên trong lớp, lọc theo mức rủi ro, sắp xếp xác suất giảm dần. | ✅ |

## Cảnh báo

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `GET` | `/api/sections/{sectionId}/warnings` | Admin hoặc Lecturer | Các cảnh báo đã gửi trong một lớp, mới nhất trước. | ✅ |
| `POST` | `/api/warnings` | Lecturer | Giảng viên gửi cảnh báo cho một sinh viên (theo EnrollmentId trong lớp mình dạy). | ✅ |
| `GET` | `/api/warnings/me` | Student | Cảnh báo của chính sinh viên đang đăng nhập, mới nhất trước. | ✅ |
| `GET` | `/api/warnings/me/unread-count` | Student | Số cảnh báo chưa đọc của sinh viên (hiện trên chuông thông báo). | ✅ |
| `PUT` | `/api/warnings/{id}/read` | Student | Sinh viên đánh dấu một cảnh báo đã đọc. | ✅ |

## Cổng sinh viên

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `GET` | `/api/students/me/risks` | Student | Mức rủi ro của sinh viên ở từng lớp, kèm các yếu tố chính và gợi ý cải thiện. | ✅ |
| `GET` | `/api/students/me/sections` | Student | Các lớp sinh viên đang học, kèm số buổi vắng, điểm tổng và mức rủi ro hiện tại. | ✅ |
| `GET` | `/api/students/me/sections/{sectionId}` | Student | Chi tiết một lớp của sinh viên: các điểm thành phần, trọng số, lịch sử điểm danh. | ✅ |

## Quản lý mô hình ML

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `GET` | `/api/ml/models` | Admin | Danh sách các phiên bản mô hình và độ chính xác (Accuracy, Precision, Recall, F1, ROC-AUC). | ✅ |
| `POST` | `/api/ml/retrain` | Admin | Yêu cầu huấn luyện lại mô hình. Chạy nền, trả 202 ngay; xem kết quả ở GET /api/ml/models. | ✅ |

## Kiểm tra hệ thống

| Method | Đường dẫn | Quyền | Mô tả | Mock |
|---|---|---|---|---|
| `GET` | `/api/health` | Công khai |  |  |
