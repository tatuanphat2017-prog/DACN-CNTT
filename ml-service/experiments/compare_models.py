"""Thí nghiệm so sánh các thuật toán cho báo cáo: bảng chỉ số + biểu đồ ROC, mức rủi ro, độ quan trọng đặc trưng.

Chạy (từ thư mục ml-service, cần cài requirements-dev.txt):
    python experiments/compare_models.py
Kết quả ghi vào docs/ml/ (model_comparison.md, results.json, các file .png).
"""
import json
import sys
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from sklearn.calibration import CalibratedClassifierCV
from sklearn.inspection import permutation_importance
from sklearn.metrics import roc_curve
from xgboost import XGBClassifier

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from app.features import FEATURES, HIGH_MIN, LOW_MAX, MONOTONIC, TARGET, risk_level   # noqa: E402
from app.training import CANDIDATES, DEPLOYABLE, compare, evaluate, prepare   # noqa: E402

OUT = ROOT.parent / "docs" / "ml"
OUT.mkdir(parents=True, exist_ok=True)
LABELS = {"absent_rate": "Tỷ lệ vắng", "late_rate": "Tỷ lệ đi trễ", "assignment_score": "Điểm bài tập",
          "midterm_score": "Điểm giữa kỳ", "prior_gpa": "GPA tích lũy"}
NAMES = {"logistic_regression": "Logistic Regression", "random_forest": "Random Forest",
         "gradient_boosting": "Gradient Boosting (sklearn)", "xgboost": "XGBoost"}

df = pd.read_csv(ROOT / "data" / "base_training.csv")
train_df, test_df = prepare(df)
positives = int(df[TARGET].sum())


def make_xgboost():
    negatives = int((train_df[TARGET] == 0).sum())
    return XGBClassifier(n_estimators=300, max_depth=4, learning_rate=0.05, subsample=0.8, colsample_bytree=0.8,
                         scale_pos_weight=negatives / max(int(train_df[TARGET].sum()), 1), eval_metric="logloss",
                         monotone_constraints=tuple(MONOTONIC), random_state=42, n_jobs=1)


candidates = {**CANDIDATES, "xgboost": make_xgboost}
results, fitted = compare(train_df, test_df, candidates)
results.sort(key=lambda r: (r["roc_auc"], r["f1"]), reverse=True)
best = results[0]["algorithm"]

# Mô hình triển khai: thuật toán tốt nhất (trong 3 thuật toán chạy được trong service) + hiệu chỉnh xác suất
deploy_name = max((r for r in results if r["algorithm"] in DEPLOYABLE), key=lambda r: (r["roc_auc"], r["f1"]))["algorithm"]
deployed = CalibratedClassifierCV(CANDIDATES[deploy_name](), method="sigmoid", cv=5).fit(train_df[FEATURES], train_df[TARGET])
proba = deployed.predict_proba(test_df[FEATURES])[:, 1]
deployed_metrics = evaluate(test_df[TARGET], proba)

# ---------- Biểu đồ 1: đường cong ROC ----------
plt.figure(figsize=(6.2, 5))
for r in results:
    fpr, tpr, _ = roc_curve(test_df[TARGET], fitted[r["algorithm"]].predict_proba(test_df[FEATURES])[:, 1])
    plt.plot(fpr, tpr, label=f"{NAMES[r['algorithm']]} (AUC = {r['roc_auc']:.3f})")
plt.plot([0, 1], [0, 1], "k--", linewidth=0.8, label="Đoán ngẫu nhiên")
plt.xlabel("Tỷ lệ dương tính giả (FPR)")
plt.ylabel("Tỷ lệ phát hiện đúng sinh viên rớt (TPR)")
plt.title("Đường cong ROC trên tập kiểm thử")
plt.legend(loc="lower right", fontsize=8)
plt.grid(alpha=0.3)
plt.tight_layout()
plt.savefig(OUT / "roc_curves.png", dpi=150)
plt.close()

# ---------- Biểu đồ 2: tỷ lệ rớt thực tế theo mức rủi ro dự đoán ----------
levels = pd.DataFrame({"level": [risk_level(p) for p in proba], "failed": test_df[TARGET].to_numpy()})
order = ["Low", "Medium", "High"]
summary = levels.groupby("level")["failed"].agg(["count", "sum"]).reindex(order).fillna(0)
rates = (summary["sum"] / summary["count"].replace(0, np.nan)).fillna(0)
plt.figure(figsize=(6, 4.2))
bars = plt.bar(["Thấp (<0.3)", "Trung bình (0.3-0.6)", "Cao (≥0.6)"], rates * 100, color=["#2e9f5b", "#e0a800", "#d64545"])
for bar, (_, row) in zip(bars, summary.iterrows()):
    plt.text(bar.get_x() + bar.get_width() / 2, bar.get_height() + 1, f"{int(row['sum'])}/{int(row['count'])}", ha="center", fontsize=9)
plt.ylabel("Tỷ lệ thực tế rớt môn (%)")
plt.title("Mức rủi ro dự đoán và tỷ lệ rớt thực tế (tập kiểm thử)")
plt.ylim(0, 110)
plt.tight_layout()
plt.savefig(OUT / "risk_levels.png", dpi=150)
plt.close()

# ---------- Biểu đồ 3: độ quan trọng đặc trưng (permutation, thang ROC-AUC) ----------
perm = permutation_importance(deployed, test_df[FEATURES], test_df[TARGET], scoring="roc_auc", n_repeats=30, random_state=42)
order_idx = np.argsort(perm.importances_mean)
plt.figure(figsize=(6.2, 3.8))
plt.barh([LABELS[FEATURES[i]] for i in order_idx], perm.importances_mean[order_idx], xerr=perm.importances_std[order_idx], color="#3b6fb6")
plt.xlabel("Mức giảm ROC-AUC khi xáo trộn đặc trưng")
plt.title(f"Độ quan trọng đặc trưng ({NAMES[deploy_name]})")
plt.grid(axis="x", alpha=0.3)
plt.tight_layout()
plt.savefig(OUT / "feature_importance.png", dpi=150)
plt.close()

# ---------- Bảng markdown ----------
rows = "\n".join(
    f"| {NAMES[r['algorithm']]} | {r['accuracy']:.3f} | {r['precision']:.3f} | {r['recall']:.3f} | {r['f1']:.3f} | "
    f"**{r['roc_auc']:.3f}** | {r['cv_roc_auc']:.3f} ± {r['cv_roc_auc_std']:.3f} |" for r in results)
level_rows = "\n".join(
    f"| {name} | {int(summary.loc[key, 'count'])} | {int(summary.loc[key, 'sum'])} | {rates[key] * 100:.1f}% |"
    for name, key in [("Thấp (< 0.3)", "Low"), ("Trung bình (0.3 – 0.6)", "Medium"), ("Cao (≥ 0.6)", "High")])
importance = "\n".join(f"| {LABELS[FEATURES[i]]} | {perm.importances_mean[i]:.3f} ± {perm.importances_std[i]:.3f} |" for i in np.argsort(-perm.importances_mean))

markdown = f"""# So sánh các mô hình dự đoán nguy cơ rớt môn

> File này sinh tự động bằng `ml-service/experiments/compare_models.py`. Chạy lại khi dữ liệu thay đổi.

## Dữ liệu và cách đánh giá

- **Nguồn:** {len(df)} đăng ký môn đã có kết quả cuối kỳ (2 học kỳ) trong dữ liệu demo của hệ thống, trong đó **{positives} rớt ({positives / len(df):.1%})**. Dữ liệu demo được **mô phỏng** (sinh viên thật không thể đưa vào đồ án) nhưng đi qua đúng các công thức tính điểm của hệ thống.
- **Bài toán:** dự đoán sớm sinh viên có rớt môn ở cuối kỳ hay không, dựa trên dữ liệu **chụp ở giữa kỳ** (nửa đầu số buổi). Lớp dương (cần phát hiện) là **rớt môn**.
- **5 đặc trưng:** tỷ lệ vắng, tỷ lệ đi trễ, điểm bài tập, điểm giữa kỳ, GPA tích lũy trước học kỳ. Không dùng điểm cuối kỳ (lúc cảnh báo chưa có) và không dùng GPA hiện tại (đã chứa kết quả môn đang dự đoán, gây rò rỉ dữ liệu).
- **Chia dữ liệu:** 80% huấn luyện ({len(train_df)} dòng) / 20% kiểm thử ({len(test_df)} dòng), chia phân tầng theo nhãn, cố định seed. Có thêm cross-validation 5 fold trên tập huấn luyện.
- **Dữ liệu thiếu:** mô phỏng thiếu ngẫu nhiên 8% điểm bài tập, 4% điểm giữa kỳ; sinh viên khóa mới tự nhiên thiếu GPA. Các mô hình đều xử lý được giá trị thiếu.
- **Mất cân bằng lớp:** dùng trọng số lớp (class_weight / scale_pos_weight) cho mọi thuật toán.
- **Ràng buộc đơn điệu:** các thuật toán dạng cây (Random Forest, Gradient Boosting, XGBoost) bị ràng buộc theo kiến thức nghiệp vụ: vắng và đi trễ càng nhiều thì xác suất rớt không giảm; điểm và GPA càng cao thì xác suất rớt không tăng. Nhờ vậy lời giải thích luôn hợp lý.
- Precision, Recall, F1 tính cho lớp "rớt" với ngưỡng 0.5.

## Kết quả (tập kiểm thử)

| Thuật toán | Accuracy | Precision | Recall | F1 | ROC-AUC | ROC-AUC (CV 5 fold) |
|---|---|---|---|---|---|---|
{rows}

Thuật toán có ROC-AUC cao nhất: **{NAMES[best]}**. Logistic Regression và XGBoost chỉ dùng làm mốc so sánh. Chỉ Random Forest và Gradient Boosting (có ràng buộc đơn điệu, chạy được bằng scikit-learn nên image nhẹ) được phép triển khai.

![ROC](roc_curves.png)

## Mô hình triển khai: {NAMES[deploy_name]} + hiệu chỉnh xác suất

Các mức rủi ro dựa trên xác suất (Thấp < 0.3, Trung bình 0.3–0.6, Cao ≥ 0.6) nên xác suất cần đáng tin. Mô hình được **hiệu chỉnh bằng Platt scaling** (sigmoid, 5 fold). Chỉ số trên tập kiểm thử sau hiệu chỉnh:

| Accuracy | Precision | Recall | F1 | ROC-AUC | Brier | Recall (ngưỡng 0.3 = Trung bình trở lên) |
|---|---|---|---|---|---|---|
| {deployed_metrics['accuracy']:.3f} | {deployed_metrics['precision']:.3f} | {deployed_metrics['recall']:.3f} | {deployed_metrics['f1']:.3f} | {deployed_metrics['roc_auc']:.3f} | {deployed_metrics['brier']:.3f} | {deployed_metrics['recall_medium_plus']:.3f} |

Khi triển khai, mô hình được huấn luyện lại trên toàn bộ dữ liệu.

### Mức rủi ro dự đoán so với thực tế

| Mức rủi ro dự đoán | Số sinh viên | Số sinh viên thực sự rớt | Tỷ lệ rớt thực tế |
|---|---|---|---|
{level_rows}

![Mức rủi ro](risk_levels.png)

### Đặc trưng nào quan trọng nhất

| Đặc trưng | Mức giảm ROC-AUC khi xáo trộn |
|---|---|
{importance}

![Độ quan trọng đặc trưng](feature_importance.png)

## Hạn chế cần nêu trong báo cáo

- Dữ liệu là dữ liệu **mô phỏng**, nên kết quả thể hiện khả năng của quy trình hơn là độ chính xác trên sinh viên thật. Khi có dữ liệu thật, chỉ cần huấn luyện lại (nút "Huấn luyện lại" của Admin).
- Điểm bài tập trong dữ liệu lịch sử là điểm cuối kỳ, được dùng làm xấp xỉ cho điểm bài tập ở giữa kỳ.
"""
(OUT / "model_comparison.md").write_text(markdown, encoding="utf-8")
(OUT / "results.json").write_text(json.dumps({"comparison": results, "deployed": {"algorithm": deploy_name, **deployed_metrics}}, ensure_ascii=False, indent=2), encoding="utf-8")

print(f"{'Thuật toán':<30}{'Acc':>7}{'Prec':>7}{'Rec':>7}{'F1':>7}{'AUC':>7}{'CV-AUC':>10}")
for r in results:
    print(f"{NAMES[r['algorithm']]:<30}{r['accuracy']:>7.3f}{r['precision']:>7.3f}{r['recall']:>7.3f}{r['f1']:>7.3f}{r['roc_auc']:>7.3f}{r['cv_roc_auc']:>10.3f}")
print("\nTriển khai:", NAMES[deploy_name], "+ hiệu chỉnh |", deployed_metrics)
print("Tỷ lệ rớt thực tế theo mức:", {k: f"{rates[k]*100:.0f}% ({int(summary.loc[k,'sum'])}/{int(summary.loc[k,'count'])})" for k in order})
print("Quan trọng nhất:", [(LABELS[FEATURES[i]], round(float(perm.importances_mean[i]), 3)) for i in np.argsort(-perm.importances_mean)])
