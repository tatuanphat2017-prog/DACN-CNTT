"""Huấn luyện, so sánh và đánh giá mô hình dự đoán nguy cơ rớt môn."""
from __future__ import annotations

import time
from datetime import datetime, timezone
from typing import Callable

import numpy as np
import pandas as pd
from sklearn.calibration import CalibratedClassifierCV
from sklearn.ensemble import HistGradientBoostingClassifier, RandomForestClassifier
from sklearn.impute import SimpleImputer
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import (accuracy_score, brier_score_loss, f1_score, precision_score,
                             recall_score, roc_auc_score)
from sklearn.model_selection import StratifiedKFold, cross_val_score, train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

from .features import FEATURES, LOW_MAX, MONOTONIC, TARGET

SEED = 42
MIN_ROWS = 60
MIN_POSITIVES = 8

# Tỷ lệ thiếu dữ liệu mô phỏng thêm vào tập huấn luyện/kiểm thử, để mô hình quen với thực tế:
# giữa kỳ có sinh viên chưa được nhập điểm bài tập hoặc giữa kỳ.
MISSING_RATES = {"assignment_score": 0.08, "midterm_score": 0.04}

# Chỉ các thuật toán có ràng buộc đơn điệu mới được triển khai. Logistic Regression chỉ là mốc so sánh (không ràng buộc được dấu hệ số).
DEPLOYABLE = ("random_forest", "gradient_boosting")

SLUGS = {"logistic_regression": "lr", "random_forest": "rf", "gradient_boosting": "gb", "xgboost": "xgb"}

CANDIDATES: dict[str, Callable[[], object]] = {
    "logistic_regression": lambda: Pipeline([
        ("imputer", SimpleImputer(strategy="median")),
        ("scaler", StandardScaler()),
        ("clf", LogisticRegression(max_iter=2000, class_weight="balanced")),
    ]),
    "random_forest": lambda: Pipeline([
        ("imputer", SimpleImputer(strategy="median")),
        ("clf", RandomForestClassifier(n_estimators=200, max_depth=8, min_samples_leaf=3, monotonic_cst=MONOTONIC,
                                       class_weight="balanced_subsample", random_state=SEED, n_jobs=1)),
    ]),
    # Gradient boosting dạng histogram của scikit-learn, xử lý được giá trị thiếu mà không cần điền
    "gradient_boosting": lambda: HistGradientBoostingClassifier(
        max_depth=4, learning_rate=0.05, max_iter=200, l2_regularization=1.0, monotonic_cst=MONOTONIC,
        class_weight="balanced", random_state=SEED),
}


def clean(df: pd.DataFrame) -> pd.DataFrame:
    """Giữ đúng các cột cần thiết, ép kiểu số, nhãn 0/1."""
    missing = [c for c in FEATURES + [TARGET] if c not in df.columns]
    if missing:
        raise ValueError(f"Thiếu cột: {', '.join(missing)}")
    out = df[FEATURES + [TARGET]].copy()
    for column in FEATURES:
        out[column] = pd.to_numeric(out[column], errors="coerce").astype(float)
    out[TARGET] = out[TARGET].astype(int)
    return out.reset_index(drop=True)


def mask_missing(df: pd.DataFrame, seed: int = SEED) -> pd.DataFrame:
    """Mô phỏng thiếu điểm bài tập / giữa kỳ ngẫu nhiên (cố định theo seed)."""
    rng = np.random.default_rng(seed)
    out = df.copy()
    for column, rate in MISSING_RATES.items():
        out.loc[rng.random(len(out)) < rate, column] = np.nan
    return out


def prepare(df: pd.DataFrame, seed: int = SEED, test_size: float = 0.2) -> tuple[pd.DataFrame, pd.DataFrame]:
    """Chia train/test có phân tầng theo nhãn, rồi mô phỏng dữ liệu thiếu ở cả hai phần."""
    df = clean(df)
    positives = int(df[TARGET].sum())
    if len(df) < MIN_ROWS or positives < MIN_POSITIVES or len(df) - positives < MIN_POSITIVES:
        raise ValueError(
            f"Không đủ dữ liệu để huấn luyện: cần ít nhất {MIN_ROWS} dòng, mỗi lớp (qua/rớt) ít nhất {MIN_POSITIVES} dòng "
            f"(hiện có {len(df)} dòng, {positives} rớt).")
    train_df, test_df = train_test_split(df, test_size=test_size, stratify=df[TARGET], random_state=seed)
    return mask_missing(train_df, seed), mask_missing(test_df, seed + 1)


def evaluate(y_true, proba) -> dict:
    """Chỉ số đánh giá, lớp dương = rớt môn. Ngưỡng phân loại 0.5; recall_medium_plus dùng ngưỡng 0.3 (mức Medium trở lên)."""
    y_true = np.asarray(y_true)
    proba = np.asarray(proba)
    pred = proba >= 0.5
    return {
        "accuracy": round(float(accuracy_score(y_true, pred)), 4),
        "precision": round(float(precision_score(y_true, pred, zero_division=0)), 4),
        "recall": round(float(recall_score(y_true, pred, zero_division=0)), 4),
        "f1": round(float(f1_score(y_true, pred, zero_division=0)), 4),
        "roc_auc": round(float(roc_auc_score(y_true, proba)), 4),
        "brier": round(float(brier_score_loss(y_true, proba)), 4),
        "recall_medium_plus": round(float(recall_score(y_true, proba >= LOW_MAX, zero_division=0)), 4),
    }


def compare(train_df: pd.DataFrame, test_df: pd.DataFrame, candidates: dict | None = None) -> tuple[list[dict], dict]:
    """Huấn luyện từng thuật toán trên tập train, đánh giá trên tập test và cross-validation 5 fold."""
    candidates = candidates or CANDIDATES
    results, fitted = [], {}
    cv = StratifiedKFold(n_splits=5, shuffle=True, random_state=SEED)
    for name, factory in candidates.items():
        started = time.perf_counter()
        model = factory()
        model.fit(train_df[FEATURES], train_df[TARGET])
        proba = model.predict_proba(test_df[FEATURES])[:, 1]
        cv_auc = cross_val_score(factory(), train_df[FEATURES], train_df[TARGET], cv=cv, scoring="roc_auc")
        results.append({
            "algorithm": name,
            **evaluate(test_df[TARGET], proba),
            "cv_roc_auc": round(float(cv_auc.mean()), 4),
            "cv_roc_auc_std": round(float(cv_auc.std()), 4),
            "seconds": round(time.perf_counter() - started, 2),
        })
        fitted[name] = model
    return results, fitted


def train(df: pd.DataFrame, existing_versions: set[str] | None = None, seed: int = SEED) -> tuple[dict, dict]:
    """
    Quy trình huấn luyện đầy đủ:
      1. Chia train/test, so sánh các thuật toán (test + cross-validation).
      2. Chọn thuật toán có ROC-AUC trên test cao nhất trong số các thuật toán được phép triển khai (DEPLOYABLE).
      3. Hiệu chỉnh xác suất (Platt/sigmoid, 5 fold) để xác suất có nghĩa, vì các ngưỡng 0.3 và 0.6 dựa trên nó.
         Chỉ số báo cáo là của mô hình đã hiệu chỉnh trên tập test chưa từng dùng để huấn luyện.
      4. Huấn luyện lại mô hình đã chọn trên TOÀN BỘ dữ liệu để triển khai.
    Trả về (artifact để lưu, meta để trả về API).
    """
    clean_df = clean(df)
    train_df, test_df = prepare(clean_df, seed)
    comparison, _ = compare(train_df, test_df)
    best = max((r for r in comparison if r["algorithm"] in DEPLOYABLE), key=lambda r: (r["roc_auc"], r["f1"]))["algorithm"]

    calibrated = CalibratedClassifierCV(CANDIDATES[best](), method="sigmoid", cv=5)
    calibrated.fit(train_df[FEATURES], train_df[TARGET])
    metrics = evaluate(test_df[TARGET], calibrated.predict_proba(test_df[FEATURES])[:, 1])

    full = mask_missing(clean_df, seed + 2)
    final_model = CalibratedClassifierCV(CANDIDATES[best](), method="sigmoid", cv=5)
    final_model.fit(full[FEATURES], full[TARGET])

    # Giá trị tham chiếu = sinh viên qua môn "điển hình" (trung vị), dùng để giải thích dự đoán
    passed = clean_df[clean_df[TARGET] == 0]
    reference = {f: round(float(passed[f].median()), 4) for f in FEATURES}

    now = datetime.now(timezone.utc)
    version = f"{SLUGS[best]}-{now:%Y%m%d-%H%M%S}"
    taken = existing_versions or set()
    counter = 1
    while version in taken:
        counter += 1
        version = f"{SLUGS[best]}-{now:%Y%m%d-%H%M%S}-{counter}"

    meta = {
        "version": version,
        "algorithm": best,
        "trained_at": now.isoformat(),
        "n_rows": int(len(clean_df)),
        "n_failed": int(clean_df[TARGET].sum()),
        "n_test": int(len(test_df)),
        "features": FEATURES,
        "metrics": metrics,
        "comparison": comparison,
    }
    artifact = {"model": final_model, "features": FEATURES, "reference": reference, "meta": meta}
    return artifact, meta
