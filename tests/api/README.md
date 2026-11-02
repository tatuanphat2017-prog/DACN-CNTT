# Kiểm thử API tích hợp

Các script gọi **API đang chạy thật** (SQL Server + backend), kiểm tra đúng/sai/trùng/sai quyền cho từng chức năng.

```bash
docker compose up -d --build          # chạy hệ thống
pip install openpyxl                  # chỉ cần cho test import Excel
python tests/api/test_admin.py        # kết quả: "N đạt, 0 lỗi"
```

- Cần file `.env` ở thư mục gốc (đọc `ADMIN_PASSWORD`, `DEMO_PASSWORD`, `IMPORT_DEFAULT_PASSWORD`).
- Đổi địa chỉ API: biến môi trường `API_BASE` (mặc định `http://localhost:8080/api`).
- Mỗi lần chạy dùng hậu tố ngẫu nhiên và tự dọn dữ liệu thử; riêng tài khoản người dùng chỉ khóa được chứ không xóa được.
