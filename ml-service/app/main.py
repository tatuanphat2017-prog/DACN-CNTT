"""ML service: dự đoán nguy cơ rớt môn. Backend (ASP.NET Core) gọi các API này, người dùng không gọi trực tiếp."""
from __future__ import annotations

import hmac
import logging
import os
import threading
from contextlib import asynccontextmanager
from pathlib import Path

import pandas as pd
from fastapi import Depends, FastAPI, Header, HTTPException

from .explain import explain_batch
from .features import FEATURES, TARGET, risk_level
from .registry import Registry
from .schemas import (ActivateRequest, Factor, PredictRequest, PredictResponse, Prediction, TrainRequest, TrainResponse)
from .training import train

BASE_DIR = Path(__file__).resolve().parent.parent
MODELS_DIR = Path(os.environ.get("MODELS_DIR", BASE_DIR / "models"))
DATA_DIR = Path(os.environ.get("DATA_DIR", BASE_DIR / "data"))
BASE_DATA = DATA_DIR / "base_training.csv"
API_KEY = os.environ.get("ML_API_KEY", "")

logger = logging.getLogger("ml-service")
registry = Registry(MODELS_DIR)
train_lock = threading.Lock()


def require_key(x_api_key: str | None = Header(default=None)) -> None:
    """Nếu đặt ML_API_KEY thì mọi API (trừ /health) phải gửi header X-API-Key khớp."""
    if API_KEY and not hmac.compare_digest(x_api_key or "", API_KEY):
        raise HTTPException(status_code=401, detail="API key không hợp lệ")


@asynccontextmanager
async def lifespan(_: FastAPI):
    if not registry.load_active():
        # Chưa có mô hình nào (lần chạy đầu): tự huấn luyện từ bộ dữ liệu nền để service dùng được ngay
        if BASE_DATA.exists():
            logger.info("Chưa có mô hình, huấn luyện từ %s", BASE_DATA)
            artifact, _ = train(pd.read_csv(BASE_DATA), registry.versions())
            registry.save(artifact)
            registry.activate(artifact["meta"]["version"])
        else:
            logger.warning("Chưa có mô hình và không có dữ liệu nền; /predict sẽ trả 503 cho tới khi gọi /train")
    yield


app = FastAPI(title="DACN ML Service", version="1.0", lifespan=lifespan)


def _require_model() -> dict:
    artifact = registry.active
    if artifact is None:
        raise HTTPException(status_code=503, detail="Chưa có mô hình. Hãy gọi /train trước.")
    return artifact


@app.get("/health")
def health() -> dict:
    return {"status": "ok", "model_version": registry.active_version}


@app.get("/model", dependencies=[Depends(require_key)])
def active_model() -> dict:
    """Thông tin mô hình đang dùng: thuật toán, chỉ số đánh giá, bảng so sánh các thuật toán."""
    return _require_model()["meta"]


@app.get("/models", dependencies=[Depends(require_key)])
def list_models() -> list[dict]:
    return registry.list()


@app.post("/predict", response_model=PredictResponse, dependencies=[Depends(require_key)])
def predict(request: PredictRequest) -> PredictResponse:
    artifact = _require_model()
    model = artifact["model"]
    frame = pd.DataFrame([item.model_dump() for item in request.items])
    probabilities = model.predict_proba(frame[FEATURES])[:, 1]
    factors = explain_batch(model, artifact["reference"], frame)

    predictions = [
        Prediction(
            id=item.id,
            probability=round(float(p), 4),
            risk_level=risk_level(float(p)),
            factors=[Factor(**f) for f in fs],
        )
        for item, p, fs in zip(request.items, probabilities, factors)
    ]
    return PredictResponse(model_version=artifact["meta"]["version"], predictions=predictions)


@app.post("/train", response_model=TrainResponse, dependencies=[Depends(require_key)])
def train_model(request: TrainRequest) -> TrainResponse:
    """Huấn luyện lại (chạy vài giây). Chỉ cho một lần huấn luyện tại một thời điểm."""
    if not train_lock.acquire(blocking=False):
        raise HTTPException(status_code=409, detail="Đang có một lần huấn luyện khác, vui lòng thử lại sau")
    try:
        frames = []
        if request.rows:
            frames.append(pd.DataFrame([r.model_dump() for r in request.rows]).rename(columns={"failed": TARGET}))
        if request.include_base and BASE_DATA.exists():
            frames.append(pd.read_csv(BASE_DATA))
        if not frames:
            raise HTTPException(status_code=400, detail="Không có dữ liệu để huấn luyện")

        try:
            artifact, meta = train(pd.concat(frames, ignore_index=True), registry.versions())
        except ValueError as error:
            raise HTTPException(status_code=400, detail=str(error)) from error

        previous = registry.active
        registry.save(artifact)
        better = previous is None or meta["metrics"]["roc_auc"] >= previous["meta"]["metrics"]["roc_auc"] - 0.01
        activated = request.activate == "always" or (request.activate == "auto" and better)
        if activated:
            registry.activate(meta["version"])
        return TrainResponse(model=meta, activated=activated, previous_version=previous["meta"]["version"] if previous else None)
    finally:
        train_lock.release()


@app.post("/activate", dependencies=[Depends(require_key)])
def activate(request: ActivateRequest) -> dict:
    try:
        return registry.activate(request.version)
    except KeyError as error:
        raise HTTPException(status_code=404, detail=f"Không có phiên bản {request.version}") from error
