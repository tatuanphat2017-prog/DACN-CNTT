# Tài liệu API

> File này sinh tự động từ `docs/api/openapi.json` bằng `docs/tools/gen_api_doc.py`. Chi tiết dữ liệu gửi/nhận xem trên Swagger (`/swagger`) của backend.

**55 endpoint**, tất cả đã có logic thật (dùng dữ liệu trong database).

## Quy ước chung

- **Đăng nhập:** `POST /api/auth/login` → lấy `accessToken`, gửi kèm mọi request cần đăng nhập: `Authorization: Bearer <accessToken>`.
- **Role:** `Admin`, `Lecturer`, `Student` (chữ, không phải số). Sai role trả **403**, chưa đăng nhập hoặc token hết hạn trả **401**.
- **Enum dạng chữ:** `RiskLevel` = `Low` | `Medium` | `High`; `AttendanceStatus` = `Present` | `Absent` | `Late` | `Excused`.
- **Phân trang:** `?page=1&pageSize=20`, kết quả `{ items, page, pageSize, totalItems }`.
- **Ngày:** `DateOnly` dạng `yyyy-MM-dd`; thời gian là UTC dạng ISO 8601.
- **Lỗi:** 400 (dữ liệu sai, kèm chi tiết từng trường), 403 (không đủ quyền với dữ liệu đó, ví dụ giảng viên xem lớp của người khác), 404 (không tìm thấy), 409 (trùng hoặc còn dữ liệu liên quan), 429 (đăng nhập quá 10 lần/phút), 503 (ML service không khả dụng).
- **Mức rủi ro:** `Low` xác suất rớt < 0.3, `Medium` 0.3–0.6, `High` > 0.6.

## Đăng nhập

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `POST` | `/api/auth/change-password` | Mọi role | Đổi mật khẩu của chính mình. Sai mật khẩu hiện tại trả 400. |
| `POST` | `/api/auth/login` | Công khai | Đăng nhập bằng username (MSSV / mã GV / admin) và mật khẩu, trả về JWT. |
| `GET` | `/api/auth/me` | Mọi role | Thông tin người đang đăng nhập (cần gửi kèm token). |

## Quản trị: tài khoản

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/users` | Admin | Danh sách người dùng, lọc theo role và từ khóa (username, họ tên hoặc email), có phân trang. |
| `POST` | `/api/users` | Admin | Tạo tài khoản. Role = Student phải có Student, Role = Lecturer phải có Lecturer. Username, email, mã sinh viên, mã giảng viên trùng trả 409. |
| `GET` | `/api/users/{id}` | Admin | Chi tiết một người dùng, kèm hồ sơ sinh viên hoặc giảng viên. |
| `PUT` | `/api/users/{id}` | Admin | Sửa thông tin. Muốn khóa tài khoản thì đặt IsActive = false (không xóa dòng dữ liệu để giữ lịch sử). Không tự khóa chính mình và không khóa admin cuối cùng. Username không đổi được. |
| `POST` | `/api/users/{id}/reset-password` | Admin | Đặt lại mật khẩu cho người dùng (Admin đặt mật khẩu mới thay họ). |

## Quản trị: sinh viên, giảng viên

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/lecturers` | Admin | Danh sách giảng viên đang hoạt động. |
| `GET` | `/api/students` | Admin | Danh sách sinh viên đang hoạt động, tìm theo mã hoặc họ tên, có phân trang. |

## Danh mục: khoa

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/departments` | Mọi role | Danh sách khoa. |
| `POST` | `/api/departments` | Admin | Thêm khoa. Mã khoa trùng trả 409. |
| `PUT` | `/api/departments/{id}` | Admin | Sửa khoa. |
| `DELETE` | `/api/departments/{id}` | Admin | Xóa khoa. Còn môn học, sinh viên hoặc giảng viên thuộc khoa thì trả 409. |

## Danh mục: môn học

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/courses` | Mọi role | Danh sách môn học, lọc theo khoa và từ khóa (mã hoặc tên), có phân trang. |
| `POST` | `/api/courses` | Admin | Thêm môn học. Mã môn trùng trả 409, khoa không tồn tại trả 400. |
| `PUT` | `/api/courses/{id}` | Admin | Sửa môn học. |
| `DELETE` | `/api/courses/{id}` | Admin | Xóa môn học. Môn đã mở lớp thì trả 409. |

## Danh mục: học kỳ

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/semesters` | Mọi role | Danh sách học kỳ, mới nhất trước. |
| `POST` | `/api/semesters` | Admin | Thêm học kỳ. Ngày kết thúc phải sau ngày bắt đầu. Đặt IsCurrent = true sẽ tự bỏ cờ của học kỳ khác. |
| `GET` | `/api/semesters/current` | Mọi role | Học kỳ hiện tại (IsCurrent = true). Chưa đặt học kỳ nào thì trả 404. |
| `PUT` | `/api/semesters/{id}` | Admin | Sửa học kỳ. |
| `DELETE` | `/api/semesters/{id}` | Admin | Xóa học kỳ. Học kỳ đã mở lớp thì trả 409. |

## Lớp học phần

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/sections` | Mọi role | Danh sách lớp học phần, lọc theo học kỳ, môn học, có phân trang. Học kỳ mới nhất trước. |
| `POST` | `/api/sections` | Admin | Mở lớp học phần. Tổng 4 trọng số phải bằng 1. Mã lớp trùng trả 409; môn, học kỳ, giảng viên không tồn tại trả 400. |
| `GET` | `/api/sections/{id}` | Mọi role | Chi tiết một lớp học phần, kèm trọng số điểm. Giảng viên chỉ xem lớp mình dạy, sinh viên chỉ xem lớp mình học (sai trả 403). |
| `PUT` | `/api/sections/{id}` | Admin | Sửa lớp học phần. Không giảm tổng số buổi xuống thấp hơn số buổi đã điểm danh. |
| `DELETE` | `/api/sections/{id}` | Admin | Xóa lớp học phần. Lớp còn sinh viên đăng ký thì trả 409 (xóa hết sinh viên khỏi lớp trước). Các buổi điểm danh còn sót được xóa theo. |
| `GET` | `/api/sections/{id}/students` | Admin hoặc Lecturer | Danh sách sinh viên trong lớp (kèm EnrollmentId, dùng cho điểm danh, điểm và cảnh báo). Giảng viên chỉ xem lớp mình dạy. |
| `POST` | `/api/sections/{id}/students` | Admin | Thêm sinh viên vào lớp. Sinh viên đã có trong lớp được bỏ qua, không báo lỗi. Id không tồn tại hoặc bị khóa trả 400. PriorGpa của từng sinh viên được tính tự động từ các học kỳ đã kết thúc trước học kỳ của lớp. |
| `DELETE` | `/api/sections/{id}/students/{enrollmentId}` | Admin | Xóa sinh viên khỏi lớp (xóa luôn điểm danh, điểm, dự đoán rủi ro, cảnh báo của sinh viên trong lớp). |

## Nhập Excel

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `POST` | `/api/import/students` | Admin | Nhập danh sách sinh viên từ file .xlsx (gửi dạng multipart/form-data, field tên "file", tối đa 5MB và 500 dòng). Dòng đầu là tiêu đề, cột bắt buộc: MSSV, Họ tên, Mã khoa, Lớp, Năm nhập học (cột Email tùy chọn). Mỗi dòng tạo một tài khoản sinh viên với username = MSSV. Mật khẩu ban đầu là cấu hình Import:DefaultPassword, nếu không cấu hình thì bằng chính MSSV (nên đổi sau). Dòng lỗi được báo trong Errors, các dòng đúng vẫn được tạo. |

## Điểm danh

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/sections/{sectionId}/attendance` | Admin hoặc Lecturer | Tóm tắt tất cả các buổi của lớp (đủ TotalSessions buổi, buổi chưa điểm danh có Date = null). |
| `GET` | `/api/sections/{sectionId}/attendance/{sessionNo}` | Admin hoặc Lecturer | Bảng điểm danh của một buổi: mỗi sinh viên một trạng thái (Present, Absent, Late, Excused). Buổi chưa điểm danh mặc định tất cả Present. |
| `PUT` | `/api/sections/{sectionId}/attendance/{sessionNo}` | Lecturer | Lưu điểm danh một buổi (tạo mới hoặc ghi đè). Sinh viên không có trong Marks được ghi mặc định Present. Backend tự tính lại AttendanceScore, TotalScore, IsPassed của cả lớp. Trả 400 nếu có EnrollmentId không thuộc lớp hoặc trùng nhau; 404 nếu số buổi ngoài khoảng 1 đến TotalSessions. |

## Điểm

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/sections/{sectionId}/grades` | Admin hoặc Lecturer | Bảng điểm cả lớp. AttendanceScore, TotalScore, IsPassed do backend tính, chỉ đọc. |
| `PUT` | `/api/sections/{sectionId}/grades` | Lecturer | Lưu điểm bài tập, giữa kỳ, cuối kỳ cho nhiều sinh viên cùng lúc. Điểm trong khoảng 0–10 (sai trả 400); điểm null nghĩa là giữ nguyên. Có đủ điểm thì backend tự tính TotalScore và IsPassed. Trả 400 nếu EnrollmentId không thuộc lớp hoặc trùng nhau. Trả về bảng điểm đã cập nhật. |

## Rủi ro (ML)

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/enrollments/{enrollmentId}/risk-history` | Admin hoặc Lecturer | Lịch sử dự đoán của một sinh viên trong lớp (theo EnrollmentId), cũ trước mới sau, dùng vẽ biểu đồ xu hướng. |
| `POST` | `/api/sections/{sectionId}/predict` | Lecturer | Chạy dự đoán cho cả lớp (gọi ML service, lưu kết quả, giữ lịch sử). Sinh viên ra mức High được hệ thống tự gửi cảnh báo (không gửi lại nếu đã có cảnh báo tự động trong 7 ngày). Trả về tổng hợp số sinh viên theo từng mức rủi ro. Trả 400 nếu lớp chưa có sinh viên hoặc chưa điểm danh buổi nào; 503 nếu ML service không khả dụng. |
| `GET` | `/api/sections/{sectionId}/risk-summary` | Admin hoặc Lecturer | Tổng hợp số sinh viên theo mức rủi ro của lớp (dùng vẽ biểu đồ dashboard), tính trên kết quả dự đoán mới nhất của từng sinh viên. |
| `GET` | `/api/sections/{sectionId}/risks` | Admin hoặc Lecturer | Kết quả dự đoán mới nhất của từng sinh viên trong lớp, lọc theo mức rủi ro, sắp xếp xác suất giảm dần. Lớp chưa từng dự đoán thì trả danh sách rỗng (gọi POST predict trước). |

## Cảnh báo

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/sections/{sectionId}/warnings` | Admin hoặc Lecturer | Các cảnh báo trong một lớp (của giảng viên và của hệ thống), mới nhất trước. |
| `POST` | `/api/warnings` | Lecturer | Giảng viên gửi cảnh báo cho một sinh viên (theo EnrollmentId trong lớp mình dạy). Nội dung tối đa 1000 ký tự. |
| `GET` | `/api/warnings/me` | Student | Cảnh báo của chính sinh viên đang đăng nhập, mới nhất trước. |
| `GET` | `/api/warnings/me/unread-count` | Student | Số cảnh báo chưa đọc của sinh viên (hiện trên chuông thông báo). |
| `PUT` | `/api/warnings/{id}/read` | Student | Sinh viên đánh dấu một cảnh báo của mình là đã đọc (gọi lại nhiều lần không sao). Cảnh báo của người khác trả 404. |

## Cổng sinh viên

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/students/me/risks` | Student | Mức rủi ro của sinh viên ở từng lớp (kết quả dự đoán mới nhất), kèm các yếu tố chính và gợi ý cải thiện. Xác suất cao trước. Lớp chưa được dự đoán thì không có trong danh sách. |
| `GET` | `/api/students/me/sections` | Student | Các lớp sinh viên đang học (mới nhất trước), kèm số buổi vắng, điểm tổng và mức rủi ro hiện tại. |
| `GET` | `/api/students/me/sections/{sectionId}` | Student | Chi tiết một lớp của sinh viên: các điểm thành phần, trọng số, lịch sử điểm danh. Lớp không đăng ký trả 404. |

## Mô hình ML

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/ml/models` | Admin | Danh sách các phiên bản mô hình và độ chính xác (Accuracy, Precision, Recall, F1, ROC-AUC), mới nhất trước. Tự đồng bộ từ ML service; nếu ML service không khả dụng thì trả dữ liệu đã lưu trong database. |
| `POST` | `/api/ml/models/{id}/activate` | Admin | Chuyển sang dùng một phiên bản mô hình đã lưu. Trả 404 nếu không có phiên bản đó, 503 nếu ML service không khả dụng. |
| `POST` | `/api/ml/retrain` | Admin | Yêu cầu huấn luyện lại mô hình bằng dữ liệu thật trong database. Chạy nền, trả 202 ngay; theo dõi ở GET /api/ml/retrain/status, phiên bản mới xuất hiện trong GET /api/ml/models. Mô hình mới được dùng ngay nếu ROC-AUC không kém mô hình hiện tại quá 0.01. Đang huấn luyện thì trả 409. |
| `GET` | `/api/ml/retrain/status` | Admin | Trạng thái lần huấn luyện lại gần nhất (Idle, Running, Succeeded, Failed) kèm thông báo kết quả. |
| `GET` | `/api/ml/training-data` | Admin | Dữ liệu huấn luyện lấy từ các đăng ký đã có kết quả cuối kỳ: đặc trưng chụp ở giữa kỳ và nhãn Failed. Không chứa thông tin định danh sinh viên (chỉ EnrollmentId). Script ml-service/scripts/export_training_data.py dùng API này. |

## Kiểm tra hệ thống

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/health` | Công khai | Kiểm tra backend, database và ML service có kết nối được không. |
