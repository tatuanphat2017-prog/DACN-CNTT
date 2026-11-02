FROM python:3.11-slim

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app ./app
# Bộ dữ liệu nền và mô hình ban đầu: service chạy được ngay, không cần huấn luyện lúc khởi động
COPY data ./data
COPY models ./models

# Chạy bằng user thường; thư mục models phải ghi được vì /train lưu phiên bản mới vào đó
RUN useradd --create-home --uid 10001 appuser && chown -R appuser /app
USER appuser

EXPOSE 8000
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
