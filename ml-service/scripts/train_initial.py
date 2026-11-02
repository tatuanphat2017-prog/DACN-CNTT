"""Huấn luyện mô hình ban đầu từ data/base_training.csv và đặt làm mô hình đang dùng.

Chạy (từ thư mục ml-service):  python scripts/train_initial.py
Kết quả nằm trong models/ (được commit để image Docker có sẵn mô hình, service chạy được ngay khi khởi động).
"""
import sys
from pathlib import Path

import pandas as pd

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from app.registry import Registry   # noqa: E402
from app.training import train   # noqa: E402

registry = Registry(ROOT / "models")
artifact, meta = train(pd.read_csv(ROOT / "data" / "base_training.csv"), registry.versions())
registry.save(artifact)
registry.activate(meta["version"])

print(f"Phiên bản {meta['version']} ({meta['algorithm']}) | {meta['n_rows']} dòng, {meta['n_failed']} rớt")
print("Chỉ số trên tập kiểm thử:", meta["metrics"])
for r in meta["comparison"]:
    print(f"  {r['algorithm']:<22} ROC-AUC {r['roc_auc']:.3f}  F1 {r['f1']:.3f}  CV-AUC {r['cv_roc_auc']:.3f}")
