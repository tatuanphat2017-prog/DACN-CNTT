# Kiểm thử API tích hợp

Các script gọi **API đang chạy thật** (SQL Server + backend), kiểm tra đúng/sai/trùng/sai quyền cho từng chức năng.

```bash
docker compose up -d --build          # chạy hệ thống
pip install openpyxl                  # chỉ cần cho test import Excel
python tests/api/test_admin.py                # quản trị, import Excel, đổi mật khẩu       (95 kiểm tra)
python tests/api/test_attendance_grades.py    # điểm danh, tính điểm, qua/rớt, cổng sinh viên   (91)
python tests/api/test_demo_data.py            # dữ liệu demo (chạy trên database mới)           (39)
python tests/api/test_risk_ml.py              # dự đoán, cảnh báo, quản lý mô hình ML           (103)
```

- `test_demo_data.py` mong đợi database vừa được tạo dữ liệu demo; các test còn lại chạy lại nhiều lần được.
- `test_risk_ml.py` gọi cả ML service thật; đặt `API_BASE_NO_ML` tới một backend không nối được ML để kiểm tra lỗi 503.
- Backend giới hạn 10 lần đăng nhập/phút nên thư viện test tự chờ và thử lại khi gặp 429.
- Cần file `.env` ở thư mục gốc (đọc `ADMIN_PASSWORD`, `DEMO_PASSWORD`, `IMPORT_DEFAULT_PASSWORD`).
- Đổi địa chỉ API: biến môi trường `API_BASE` (mặc định `http://localhost:8080/api`).
- Mỗi lần chạy dùng hậu tố ngẫu nhiên và tự dọn dữ liệu thử; riêng tài khoản người dùng chỉ khóa được chứ không xóa được.
