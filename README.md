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

- Server name: `localhost,1433`
- Authentication: SQL Server Authentication
- Login: `sa` – Password: giá trị `MSSQL_SA_PASSWORD` trong `.env`
- Tick **Trust server certificate**

## Quy tắc Git

- `main`: bản ổn định, đang deploy. `develop`: bản tích hợp.
- Mỗi chức năng một nhánh tách từ `develop`: `feature/be-...`, `feature/fe-...`, sửa lỗi: `fix/...`.
- Làm xong tạo Pull Request vào `develop`, người còn lại review rồi mới merge. Không push thẳng lên `main`.
- Commit theo dạng `feat: ...`, `fix: ...`, `docs: ...`.
- Không commit file `.env` hay mật khẩu.
