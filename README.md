# DACN-CNTT – Hệ thống hỗ trợ học tập & cảnh báo sinh viên có nguy cơ rớt môn

Đồ án chuyên ngành – Công nghệ phần mềm.

| Thành viên | Phụ trách |
|---|---|
| Phát | Backend (ASP.NET Core), Database (SQL Server), ML service, DevOps |
| Huy | Frontend (React) |

## Kiến trúc

```
Frontend (React) ──► Backend (ASP.NET Core 10) ──► SQL Server 2022
                              │
                              └──► ML Service (Python FastAPI)
```

| Thư mục | Nội dung |
|---|---|
| `backend/` | ASP.NET Core Web API + EF Core |
| `ml-service/` | FastAPI phục vụ mô hình dự đoán |
| `frontend/` | React + Vite + TailwindCSS |
| `docs/` | Báo cáo, use case, ERD |

## Chạy toàn bộ hệ thống bằng Docker

Yêu cầu: Docker Desktop đang chạy.

1. Tạo file `.env` từ mẫu và đặt mật khẩu SQL Server:
   ```bash
   cp .env.example .env
   ```
2. Build và chạy:
   ```bash
   docker compose up -d --build
   ```
3. Kiểm tra:
   - Swagger: http://localhost:8080/swagger
   - Health: http://localhost:8080/api/health → `{"backend":true,"database":true,"mlService":true}`
   - ML service: http://localhost:8000/docs

Dừng hệ thống: `docker compose down` (dữ liệu DB vẫn giữ trong volume).

## Kết nối SQL Server bằng SSMS

- Server name: `127.0.0.1,1433` (không dùng `localhost` vì có thể bị phân giải sang IPv6 và bị từ chối)
- Authentication: SQL Server Authentication
- Login: `sa` – Password: giá trị `MSSQL_SA_PASSWORD` trong `.env`
- Tick **Trust server certificate**

## Đăng nhập & phân quyền (JWT)

- Lần chạy đầu tiên backend tự tạo tài khoản **admin** với mật khẩu `ADMIN_PASSWORD` trong `.env`.
- `POST /api/auth/login` với `{"username": "...", "password": "..."}` → trả về `accessToken` (hết hạn sau 8 giờ) và thông tin người dùng.
- Gọi API cần đăng nhập: gửi header `Authorization: Bearer <accessToken>`.
- `GET /api/auth/me` → thông tin người đang đăng nhập.
- Token chứa các claim `sub` (id), `name` (username), `role` (`Admin` | `Lecturer` | `Student`).
- Trên Swagger: gọi login, copy `accessToken`, bấm nút **Authorize** 🔒 và dán vào.
- Sai quá 10 lần/phút/IP → bị chặn tạm thời (HTTP 429).

Chạy backend bằng F5 (Visual Studio) cần đặt thêm 2 user-secrets (chạy trong `backend/DACN.Api`):

```bash
dotnet user-secrets set "Jwt:Key" "<chuỗi ngẫu nhiên ≥ 32 ký tự, giống JWT_KEY trong .env>"
dotnet user-secrets set "Seed:AdminPassword" "<giống ADMIN_PASSWORD trong .env>"
```

## API cho Frontend

- Swagger (xem và gọi thử): http://localhost:8080/swagger — nút **Authorize** để dán token.
- Bảng tóm tắt 51 endpoint, quyền của từng role: [docs/API.md](docs/API.md).
- Đặc tả OpenAPI: [docs/api/openapi.json](docs/api/openapi.json) (có thể dùng sinh kiểu TypeScript, ví dụ `npx openapi-typescript docs/api/openapi.json -o src/api/schema.d.ts`).
- Phần lớn API đang **MOCK**: trả dữ liệu mẫu cố định (response có header `X-Mock-Data: true`, Swagger ghi rõ). Backend sẽ thay dần bằng logic thật, **hình dạng dữ liệu giữ nguyên** nên Frontend không phải sửa.
- Dữ liệu mẫu: lớp `IT001.01` có 8 sinh viên với đủ 3 mức rủi ro (4 Low, 2 Medium, 2 High).

**Dữ liệu demo thật** (tự tạo lần chạy đầu khi đặt `DEMO_PASSWORD` trong `.env`): 6 giảng viên, 120 sinh viên, 16 môn, 3 học kỳ (2 kỳ đã kết thúc có đủ điểm và kết quả qua/rớt, 1 kỳ hiện tại đang học giữa kỳ), 48 lớp, 1.800 lượt đăng ký, khoảng 22.800 lượt điểm danh. Dữ liệu sinh bằng mô phỏng cố định (cùng một kết quả mỗi lần), tỷ lệ rớt khoảng 17%. Hai học kỳ đã kết thúc là lịch sử để huấn luyện mô hình ML.

| Role | Username | Mật khẩu |
|---|---|---|
| Admin | `admin` | `ADMIN_PASSWORD` |
| Giảng viên | `GV001` … `GV006` | `DEMO_PASSWORD` |
| Sinh viên | `SV001` … `SV120` | `DEMO_PASSWORD` |

> Dữ liệu demo chỉ được tạo khi database **chưa có lớp học phần nào**. Nếu database cũ đã có dữ liệu thử và muốn tạo lại dữ liệu demo mới: `docker compose down -v` (⚠️ xóa toàn bộ database dev trong Docker) rồi `docker compose up -d --build`.

## ML service (dự đoán nguy cơ rớt môn)

Service Python (FastAPI) trong [ml-service/](ml-service/README.md): dự đoán xác suất rớt môn từ 5 đặc trưng chụp ở giữa kỳ, kèm lời giải thích. So sánh thuật toán và biểu đồ: [docs/ml/model_comparison.md](docs/ml/model_comparison.md). Chạy test: `cd ml-service && pip install -r requirements-dev.txt && pytest tests -q`.

## Database & migration (backend)

Thiết kế CSDL: [docs/ERD.md](docs/ERD.md). Bảng được tạo bằng EF Core Code First; backend tự áp dụng migration khi khởi động.

Sau khi sửa entity trong `backend/DACN.Api/Entities` hoặc `AppDbContext`, tạo migration mới (chạy trong thư mục `backend/`):

```bash
dotnet tool restore
dotnet ef migrations add <TenMigration> --project DACN.Api
dotnet ef database update --project DACN.Api
```

## Quy tắc Git

- `main`: bản ổn định, đang deploy. `develop`: bản tích hợp.
- Mỗi chức năng một nhánh tách từ `develop`: `feature/be-...`, `feature/fe-...`, sửa lỗi: `fix/...`.
- Làm xong tạo Pull Request vào `develop`, người còn lại review rồi mới merge. Không push thẳng lên `main`.
- Commit theo dạng `feat: ...`, `fix: ...`, `docs: ...`.
- Không commit file `.env` hay mật khẩu.
