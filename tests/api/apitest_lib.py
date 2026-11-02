"""Thư viện test API tích hợp: gọi API đang chạy, đăng nhập các role, kiểm tra kết quả.

Yêu cầu: hệ thống đang chạy (docker compose up -d), có file .env ở thư mục gốc repo.
Đổi địa chỉ API bằng biến môi trường API_BASE (mặc định http://localhost:8080/api).
"""
import io
import json
import os
import time
from pathlib import Path
import urllib.error
import urllib.request
import uuid

BASE = os.environ.get("API_BASE", "http://localhost:8080/api")
ENV = {}
for line in open(Path(__file__).resolve().parents[2] / ".env", encoding="utf-8"):
    if "=" in line and not line.startswith("#"):
        k, v = line.rstrip("\n").split("=", 1)
        ENV[k] = v

passed = 0
failed = 0


def call(method, path, body=None, token=None, raw=None, content_type=None):
    data = raw if raw is not None else (json.dumps(body).encode("utf-8") if body is not None else None)
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", content_type or "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            text = r.read().decode("utf-8")
            return r.status, (json.loads(text) if text else None), dict(r.headers)
    except urllib.error.HTTPError as e:
        text = e.read().decode("utf-8")
        try:
            return e.code, (json.loads(text) if text else None), dict(e.headers)
        except ValueError:
            return e.code, text, dict(e.headers)


def login(username, password):
    """Đăng nhập, trả token hoặc None nếu sai thông tin. Backend giới hạn 10 lần đăng nhập/phút/IP (429) nên tự chờ và thử lại."""
    for _ in range(15):
        status, body, _ = call("POST", "/auth/login", {"username": username, "password": password})
        if status != 429:
            return body["accessToken"] if status == 200 else None
        time.sleep(5)
    raise RuntimeError("Đăng nhập liên tục bị giới hạn tốc độ (429)")


def multipart(field, filename, content, mime="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"):
    boundary = uuid.uuid4().hex
    out = io.BytesIO()
    out.write(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{field}\"; filename=\"{filename}\"\r\nContent-Type: {mime}\r\n\r\n".encode())
    out.write(content)
    out.write(f"\r\n--{boundary}--\r\n".encode())
    return out.getvalue(), f"multipart/form-data; boundary={boundary}"


def check(name, expected, actual):
    global passed, failed
    ok = expected == actual
    passed += ok
    failed += (not ok)
    print(f"  {'PASS' if ok else 'FAIL'}  {name:<70} {actual if ok else f'mong đợi {expected!r}, thực tế {actual!r}'}")
    return ok


def summary():
    print(f"\nKết quả: {passed} đạt, {failed} lỗi (tổng {passed + failed})")
    return failed == 0
