"""Giải thích dự đoán: mỗi đặc trưng làm tăng hay giảm nguy cơ rớt bao nhiêu."""
from __future__ import annotations

import pandas as pd

from .features import FEATURES


def explain_batch(model, reference: dict, X: pd.DataFrame, top: int = 3, min_impact: float = 0.01) -> list[list[dict]]:
    """
    Phương pháp thay thế từng đặc trưng (occlusion): với mỗi đặc trưng, thay giá trị của sinh viên bằng giá trị tham chiếu
    (sinh viên qua môn điển hình) rồi xem xác suất rớt thay đổi bao nhiêu.
        contribution = P(rớt | giá trị thật) - P(rớt | đặc trưng đó ở mức điển hình)
    Dương: đặc trưng đang làm tăng nguy cơ ("increase"). Âm: đang làm giảm nguy cơ ("decrease").
    Nhẹ hơn nhiều so với SHAP nhưng đủ cho lời giải thích ngắn trên giao diện. Tính cả lô trong một lần gọi model.
    Đặc trưng bị thiếu (NaN) không được đưa vào danh sách: không thể quy nguyên nhân cho dữ liệu không có.
    Trả về, cho mỗi dòng, tối đa `top` yếu tố có ảnh hưởng lớn nhất (Impact >= min_impact; luôn có ít nhất 1 yếu tố).
    """
    n = len(X)
    base_frame = X[FEATURES].reset_index(drop=True)
    frames = [base_frame] + [base_frame.assign(**{feature: reference[feature]}) for feature in FEATURES]
    proba = model.predict_proba(pd.concat(frames, ignore_index=True))[:, 1]

    results = []
    for i in range(n):
        contributions = [(feature, float(proba[i] - proba[n * (j + 1) + i])) for j, feature in enumerate(FEATURES)
                         if pd.notna(base_frame.at[i, feature])]
        contributions.sort(key=lambda item: abs(item[1]), reverse=True)
        chosen = [c for c in contributions[:top] if abs(c[1]) >= min_impact] or contributions[:1]
        results.append([
            {"feature": feature, "impact": round(abs(value), 4), "direction": "increase" if value > 0 else "decrease"}
            for feature, value in chosen
        ])
    return results
