# ML service – dự đoán nguy cơ rớt môn

Service Python (FastAPI) trả lời câu hỏi: **"sinh viên này, ở giữa kỳ, có nguy cơ rớt môn ở cuối kỳ không, và vì sao?"**. Backend ASP.NET Core gọi service này; người dùng không gọi trực tiếp.

## Mô hình hoạt động thế nào

| Thành phần | Nội dung |
|---|---|
| **Bài toán** | Phân loại nhị phân, lớp dương = **rớt môn**. Đầu ra là xác suất rớt (0–1) |
| **5 đặc trưng** | tỷ lệ vắng, tỷ lệ đi trễ, điểm bài tập, điểm giữa kỳ, GPA tích lũy trước học kỳ (xem `app/features.py`) |
| **Mức rủi ro** | Thấp < 0.3, Trung bình 0.3–0.6, Cao ≥ 0.6 |
| **Dữ liệu thiếu** | Điểm bài tập, giữa kỳ, GPA có thể thiếu; mô hình xử lý được và được huấn luyện với dữ liệu thiếu mô phỏng |
| **Thuật toán** | So sánh Logistic Regression, Random Forest, Gradient Boosting (và XGBoost trong thí nghiệm). Chọn thuật toán tốt nhất trong nhóm có ràng buộc đơn điệu (Random Forest, Gradient Boosting) |
| **Ràng buộc đơn điệu** | Vắng/trễ nhiều hơn không thể giảm rủi ro; điểm/GPA cao hơn không thể tăng rủi ro. Kiểm thử bằng pytest |
| **Hiệu chỉnh xác suất** | Platt scaling (sigmoid, 5 fold) để xác suất đáng tin, vì các mức rủi ro dựa trên ngưỡng xác suất |
| **Giải thích** | Với mỗi đặc trưng, thay giá trị bằng mức của "sinh viên qua môn điển hình" rồi xem xác suất đổi bao nhiêu (occlusion). Đặc trưng bị thiếu không bị quy lỗi |

Kết quả so sánh thuật toán, biểu đồ ROC, độ quan trọng đặc trưng: [../docs/ml/model_comparison.md](../docs/ml/model_comparison.md).

## API

| Method | Đường dẫn | Mô tả |
|---|---|---|
| GET | `/health` | Kiểm tra sống, kèm phiên bản mô hình (không cần API key) |
| GET | `/model` | Thông tin mô hình đang dùng: thuật toán, chỉ số, bảng so sánh |
| GET | `/models` | Mọi phiên bản đã lưu, đánh dấu bản đang dùng |
| POST | `/predict` | Dự đoán cho tối đa 500 sinh viên một lần, trả xác suất, mức rủi ro, tối đa 3 yếu tố chính |
| POST | `/train` | Huấn luyện lại (vài giây). `activate`: `auto` (dùng ngay nếu ROC-AUC không kém bản hiện tại quá 0.01), `always`, `never` |
| POST | `/activate` | Đổi sang một phiên bản đã lưu |

Trang tài liệu tương tác khi chạy: http://localhost:8000/docs. Nếu đặt biến môi trường `ML_API_KEY` thì mọi API (trừ `/health`) phải gửi header `X-API-Key`.

## Chạy

**Bằng Docker (khuyên dùng):** `docker compose up -d --build` ở thư mục gốc repo. Image đã có sẵn mô hình ban đầu và dữ liệu nền. Các phiên bản huấn luyện lại lưu trong volume `ml-models`.

> Volume giữ mô hình đang dùng qua các lần build lại. Muốn quay về mô hình đóng gói sẵn của image mới: `docker compose down -v` (xóa cả database dev), hoặc gọi `POST /train` với `"activate": "always"`.

**Trực tiếp để phát triển:**

```bash
cd ml-service
python -m venv .venv
.venv/Scripts/pip install -r requirements-dev.txt     # Linux/macOS: .venv/bin/pip
.venv/Scripts/python -m uvicorn app.main:app --port 8000
.venv/Scripts/python -m pytest tests -q               # 41 kiểm thử
```

## Dữ liệu và huấn luyện

- `data/base_training.csv`: bộ dữ liệu nền (1.200 dòng từ 2 học kỳ đã kết thúc của dữ liệu demo). Tạo lại bằng `python scripts/export_training_data.py` khi backend đang chạy.
- `scripts/train_initial.py`: huấn luyện mô hình ban đầu lưu vào `models/` (được commit để image chạy được ngay).
- `experiments/compare_models.py`: thí nghiệm so sánh, ghi bảng và biểu đồ vào `docs/ml/`.
- Huấn luyện lại từ dữ liệu thật của hệ thống: Admin bấm "Huấn luyện lại" (backend lấy dữ liệu từ database qua `GET /api/ml/training-data` rồi gọi `/train`).
- Mô hình lưu dạng `.joblib`: **ghim đúng phiên bản scikit-learn** trong `requirements.txt` và chỉ nạp file do chính service tạo.

## Hạn chế

- Dữ liệu demo là **mô phỏng**, nên chỉ số thể hiện quy trình hơn là độ chính xác trên sinh viên thật.
- Điểm bài tập trong dữ liệu lịch sử là điểm cuối kỳ, dùng xấp xỉ cho điểm bài tập ở giữa kỳ.
- Trên Render gói miễn phí không có ổ đĩa bền: phiên bản huấn luyện lại mất khi service khởi động lại và quay về mô hình đóng gói sẵn.
