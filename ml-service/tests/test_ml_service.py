import threading

import numpy as np
import pandas as pd
import pytest

from app import main
from app.features import FEATURES, risk_level
from app.training import MIN_ROWS, clean, mask_missing, prepare
from conftest import BASE, make_dataset

GOOD = {"id": 1, "absent_rate": 0.0, "late_rate": 0.0, "assignment_score": 9.0, "midterm_score": 9.0, "prior_gpa": 3.7}
BAD = {"id": 2, "absent_rate": 0.5, "late_rate": 0.1, "assignment_score": 2.5, "midterm_score": 2.0, "prior_gpa": 1.2}


def test_dataset_fixture_is_realistic():
    share = BASE["failed"].mean()
    assert 0.08 < share < 0.35, f"dữ liệu mô phỏng ra {share:.0%} rớt"


# ---------- Mức rủi ro ----------

@pytest.mark.parametrize("p,level", [(0.0, "Low"), (0.299, "Low"), (0.3, "Medium"), (0.599, "Medium"), (0.6, "High"), (1.0, "High")])
def test_risk_level_boundaries(p, level):
    assert risk_level(p) == level


# ---------- Huấn luyện ----------

def test_prepare_rejects_tiny_data():
    with pytest.raises(ValueError, match="Không đủ dữ liệu"):
        prepare(make_dataset(MIN_ROWS - 10))


def test_prepare_rejects_single_class():
    df = make_dataset(200)
    df["failed"] = 0
    with pytest.raises(ValueError, match="Không đủ dữ liệu"):
        prepare(df)


def test_prepare_requires_columns():
    with pytest.raises(ValueError, match="Thiếu cột"):
        clean(pd.DataFrame({"absent_rate": [0.1]}))


def test_mask_missing_is_deterministic_and_only_hits_scores():
    df = make_dataset(400)
    a, b = mask_missing(df, seed=7), mask_missing(df, seed=7)
    pd.testing.assert_frame_equal(a, b)
    assert a["assignment_score"].isna().sum() > 0
    assert a["absent_rate"].isna().sum() == 0 and a["late_rate"].isna().sum() == 0


# ---------- API ----------

def test_health_reports_model(client):
    body = client.get("/health").json()
    assert body["status"] == "ok" and body["model_version"]


def test_active_model_metadata(client):
    meta = client.get("/model").json()
    assert meta["features"] == FEATURES
    assert {"accuracy", "precision", "recall", "f1", "roc_auc", "brier"} <= meta["metrics"].keys()
    assert meta["metrics"]["roc_auc"] > 0.75, "mô hình phải học được quy luật trong dữ liệu mô phỏng"
    assert {c["algorithm"] for c in meta["comparison"]} == {"logistic_regression", "random_forest", "gradient_boosting"}
    assert meta["algorithm"] in ("random_forest", "gradient_boosting"), "chỉ thuật toán có ràng buộc đơn điệu được triển khai"


def test_predict_shape_and_consistency(client):
    r = client.post("/predict", json={"items": [GOOD, BAD]})
    assert r.status_code == 200
    body = r.json()
    assert body["model_version"] == client.get("/health").json()["model_version"]
    assert [p["id"] for p in body["predictions"]] == [1, 2]
    for p in body["predictions"]:
        assert 0 <= p["probability"] <= 1
        assert p["risk_level"] == risk_level(p["probability"])
        assert 1 <= len(p["factors"]) <= 3
        assert all(f["direction"] in ("increase", "decrease") and f["feature"] in FEATURES and f["impact"] >= 0 for f in p["factors"])


def test_risky_student_scores_higher_and_is_explained(client):
    good, bad = client.post("/predict", json={"items": [GOOD, BAD]}).json()["predictions"]
    assert bad["probability"] > good["probability"] + 0.3
    assert good["risk_level"] == "Low"
    assert bad["risk_level"] in ("Medium", "High")
    assert bad["factors"][0]["direction"] == "increase", "yếu tố chính của sinh viên rủi ro phải làm tăng nguy cơ"
    impacts = [f["impact"] for f in bad["factors"]]
    assert impacts == sorted(impacts, reverse=True)


def test_more_absences_never_lowers_risk(client):
    items = [{**GOOD, "id": i, "absent_rate": rate} for i, rate in enumerate([0.0, 0.1, 0.2, 0.3, 0.5])]
    probs = [p["probability"] for p in client.post("/predict", json={"items": items}).json()["predictions"]]
    assert probs == sorted(probs)


GRIDS = {"absent_rate": np.linspace(0, 0.6, 13), "late_rate": np.linspace(0, 0.6, 13), "assignment_score": np.linspace(0, 10, 21),
         "midterm_score": np.linspace(0, 10, 21), "prior_gpa": np.linspace(0, 4, 17)}


@pytest.mark.parametrize("feature,direction", [("absent_rate", 1), ("late_rate", 1), ("assignment_score", -1), ("midterm_score", -1), ("prior_gpa", -1)])
def test_risk_is_monotonic_in_every_feature(client, feature, direction):
    """Vắng/trễ nhiều hơn không được làm giảm rủi ro; điểm và GPA cao hơn không được làm tăng rủi ro."""
    base = {"absent_rate": 0.15, "late_rate": 0.1, "assignment_score": 6.0, "midterm_score": 6.0, "prior_gpa": 2.5}
    items = [{**base, "id": i, feature: float(v)} for i, v in enumerate(GRIDS[feature])]
    probs = [p["probability"] for p in client.post("/predict", json={"items": items}).json()["predictions"]]
    assert (np.diff(probs) * direction >= -1e-9).all(), f"{feature}: {probs}"


def test_explanations_never_contradict_common_sense(client):
    """Sinh viên điểm/GPA thấp: các yếu tố điểm/GPA phải 'increase'; điểm/GPA cao: phải 'decrease'."""
    low = {"id": 1, "absent_rate": 0.0, "late_rate": 0.0, "assignment_score": 2.0, "midterm_score": 2.0, "prior_gpa": 1.0}
    high = {"id": 2, "absent_rate": 0.0, "late_rate": 0.0, "assignment_score": 9.5, "midterm_score": 9.5, "prior_gpa": 3.9}
    p_low, p_high = client.post("/predict", json={"items": [low, high]}).json()["predictions"]
    for factor in p_low["factors"]:
        assert factor["direction"] == "increase", factor
    for factor in p_high["factors"]:
        assert factor["direction"] == "decrease", factor


def test_missing_features_are_not_blamed(client):
    item = {"id": 1, "absent_rate": 0.4, "late_rate": 0.0, "assignment_score": None, "midterm_score": None, "prior_gpa": None}
    factors = client.post("/predict", json={"items": [item]}).json()["predictions"][0]["factors"]
    assert factors and {f["feature"] for f in factors} <= {"absent_rate", "late_rate"}


def test_predict_handles_missing_values(client):
    item = {"id": 5, "absent_rate": 0.1, "late_rate": 0.0, "assignment_score": None, "midterm_score": None, "prior_gpa": None}
    r = client.post("/predict", json={"items": [item]})
    assert r.status_code == 200
    assert 0 <= r.json()["predictions"][0]["probability"] <= 1


@pytest.mark.parametrize("bad", [
    {"absent_rate": 1.5}, {"absent_rate": -0.1}, {"late_rate": 2}, {"assignment_score": 11}, {"midterm_score": -1}, {"prior_gpa": 4.5},
])
def test_predict_validates_ranges(client, bad):
    assert client.post("/predict", json={"items": [{**GOOD, **bad}]}).status_code == 422


def test_predict_validates_batch_size(client):
    assert client.post("/predict", json={"items": []}).status_code == 422
    assert client.post("/predict", json={"items": [{**GOOD, "id": i} for i in range(501)]}).status_code == 422


def test_predict_batch_matches_single(client):
    both = client.post("/predict", json={"items": [GOOD, BAD]}).json()["predictions"]
    single = client.post("/predict", json={"items": [BAD]}).json()["predictions"][0]
    assert both[1]["probability"] == pytest.approx(single["probability"], abs=1e-4)


# ---------- Huấn luyện lại, đổi phiên bản ----------

def test_train_activate_and_switch_versions(client, extra_rows):
    old_version = client.get("/model").json()["version"]

    r = client.post("/train", json={"rows": extra_rows, "include_base": True, "activate": "never"})
    assert r.status_code == 200
    body = r.json()
    new_version = body["model"]["version"]
    assert body["activated"] is False and body["previous_version"] == old_version
    assert new_version != old_version
    assert body["model"]["n_rows"] == len(extra_rows) + len(BASE)
    assert client.get("/model").json()["version"] == old_version, "activate=never không được đổi mô hình đang dùng"

    versions = {m["version"]: m["is_active"] for m in client.get("/models").json()}
    assert versions[old_version] is True and versions[new_version] is False

    assert client.post("/activate", json={"version": new_version}).status_code == 200
    assert client.get("/model").json()["version"] == new_version
    assert client.get("/health").json()["model_version"] == new_version

    assert client.post("/activate", json={"version": old_version}).status_code == 200
    assert client.get("/model").json()["version"] == old_version


def test_train_always_activates(client, extra_rows):
    r = client.post("/train", json={"rows": extra_rows[:200], "include_base": True, "activate": "always"}).json()
    assert r["activated"] is True
    assert client.get("/model").json()["version"] == r["model"]["version"]


def test_train_rejects_insufficient_data(client):
    rows = [{**GOOD, "failed": False} for _ in range(10)]
    for row in rows:
        row.pop("id")
    r = client.post("/train", json={"rows": rows, "include_base": False})
    assert r.status_code == 400 and "Không đủ dữ liệu" in r.json()["detail"]


def test_train_without_any_data(client):
    assert client.post("/train", json={"rows": [], "include_base": False}).status_code == 400


def test_train_conflict_when_busy(client):
    assert main.train_lock.acquire(blocking=False)
    try:
        assert client.post("/train", json={"rows": [], "include_base": True}).status_code == 409
    finally:
        main.train_lock.release()


def test_activate_unknown_version(client):
    assert client.post("/activate", json={"version": "khong-co"}).status_code == 404


def test_versions_are_unique_when_trained_in_same_second(client, extra_rows):
    first = client.post("/train", json={"rows": extra_rows, "activate": "never"}).json()["model"]["version"]
    second = client.post("/train", json={"rows": extra_rows, "activate": "never"}).json()["model"]["version"]
    assert first != second


# ---------- Khóa API ----------

def test_api_key_protects_everything_but_health(client, monkeypatch):
    monkeypatch.setattr(main, "API_KEY", "bi-mat")
    payload = {"items": [GOOD]}
    assert client.get("/health").status_code == 200
    assert client.post("/predict", json=payload).status_code == 401
    assert client.post("/predict", json=payload, headers={"X-API-Key": "sai"}).status_code == 401
    assert client.post("/predict", json=payload, headers={"X-API-Key": "bi-mat"}).status_code == 200
    assert client.get("/model").status_code == 401
    assert client.post("/train", json={"rows": []}).status_code == 401
    assert client.post("/activate", json={"version": "x"}).status_code == 401


def test_predict_is_thread_safe_while_switching(client, extra_rows):
    """Nhiều request dự đoán song song với việc đổi phiên bản không được lỗi."""
    versions = [m["version"] for m in client.get("/models").json()][:2]
    errors = []

    def predict_many():
        for _ in range(15):
            if client.post("/predict", json={"items": [GOOD, BAD]}).status_code != 200:
                errors.append("predict")

    def switch_many():
        for i in range(10):
            if client.post("/activate", json={"version": versions[i % len(versions)]}).status_code != 200:
                errors.append("activate")

    threads = [threading.Thread(target=predict_many) for _ in range(3)] + [threading.Thread(target=switch_many)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    assert not errors
