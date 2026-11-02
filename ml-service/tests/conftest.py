"""Cấu hình test: dùng thư mục tạm cho mô hình và dữ liệu, dữ liệu mô phỏng nhỏ để train nhanh."""
import os
import sys
import tempfile
from pathlib import Path

import numpy as np
import pandas as pd
import pytest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

# Phải đặt biến môi trường TRƯỚC KHI import app.main (module đọc chúng lúc import)
TMP = Path(tempfile.mkdtemp(prefix="ml-service-test-"))
(TMP / "models").mkdir()
(TMP / "data").mkdir()
os.environ["MODELS_DIR"] = str(TMP / "models")
os.environ["DATA_DIR"] = str(TMP / "data")
os.environ.pop("ML_API_KEY", None)


def make_dataset(n: int, seed: int = 0) -> pd.DataFrame:
    """Dữ liệu mô phỏng: rớt khi vắng nhiều, điểm thấp, GPA thấp (ra khoảng 15-25% rớt)."""
    rng = np.random.default_rng(seed)
    absent = np.clip(rng.beta(1.2, 8, n), 0, 1)
    late = np.clip(rng.beta(1.2, 10, n), 0, 1)
    gpa = np.clip(rng.normal(2.7, 0.6, n), 0, 4)
    assignment = np.clip(rng.normal(6.5 + 0.8 * (gpa - 2.7), 1.4, n), 0, 10)
    midterm = np.clip(rng.normal(6.2 + 0.9 * (gpa - 2.7), 1.6, n), 0, 10)
    logit = -2.4 + 11 * absent - 0.6 * (midterm - 6) - 0.4 * (assignment - 6.5) - 0.8 * (gpa - 2.7)
    failed = rng.random(n) < 1 / (1 + np.exp(-logit))
    return pd.DataFrame({
        "absent_rate": absent.round(3), "late_rate": late.round(3), "assignment_score": assignment.round(1),
        "midterm_score": midterm.round(1), "prior_gpa": gpa.round(2), "failed": failed.astype(int),
    })


BASE = make_dataset(500, seed=1)
BASE.to_csv(TMP / "data" / "base_training.csv", index=False)


@pytest.fixture(scope="session")
def client():
    from fastapi.testclient import TestClient
    from app.main import app

    with TestClient(app) as c:    # chạy lifespan: tự huấn luyện mô hình từ base_training.csv
        yield c


@pytest.fixture(scope="session")
def extra_rows():
    df = make_dataset(300, seed=2)
    return [{**{k: (None if pd.isna(v) else float(v)) for k, v in row.items() if k != "failed"}, "failed": bool(row["failed"])}
            for row in df.to_dict("records")]
