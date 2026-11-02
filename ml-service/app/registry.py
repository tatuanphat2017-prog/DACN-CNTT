"""Lưu, liệt kê và chọn mô hình đang dùng. Mỗi phiên bản gồm <version>.joblib (mô hình) và <version>.json (thông tin)."""
from __future__ import annotations

import json
import threading
from pathlib import Path

import joblib


class Registry:
    def __init__(self, models_dir: Path):
        self.dir = Path(models_dir)
        self.dir.mkdir(parents=True, exist_ok=True)
        self._lock = threading.RLock()
        self._artifact: dict | None = None

    # ----- mô hình đang dùng -----

    @property
    def active_version(self) -> str | None:
        return self._artifact["meta"]["version"] if self._artifact else None

    @property
    def active(self) -> dict | None:
        return self._artifact

    def load_active(self) -> bool:
        """Nạp mô hình ghi trong active.json. Trả False nếu chưa có mô hình nào."""
        pointer = self.dir / "active.json"
        if not pointer.exists():
            return False
        version = json.loads(pointer.read_text(encoding="utf-8")).get("version")
        if not version or not (self.dir / f"{version}.joblib").exists():
            return False
        with self._lock:
            self._artifact = joblib.load(self.dir / f"{version}.joblib")
        return True

    def activate(self, version: str) -> dict:
        path = self.dir / f"{version}.joblib"
        if not path.exists():
            raise KeyError(version)
        artifact = joblib.load(path)
        with self._lock:
            self._artifact = artifact
            (self.dir / "active.json").write_text(json.dumps({"version": version}), encoding="utf-8")
        return artifact["meta"]

    # ----- lưu và liệt kê -----

    def versions(self) -> set[str]:
        return {p.stem for p in self.dir.glob("*.joblib")}

    def save(self, artifact: dict) -> dict:
        meta = artifact["meta"]
        with self._lock:
            joblib.dump(artifact, self.dir / f"{meta['version']}.joblib", compress=3)
            (self.dir / f"{meta['version']}.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
        return meta

    def list(self) -> list[dict]:
        metas = []
        for path in self.dir.glob("*.json"):
            if path.name == "active.json":
                continue
            metas.append(json.loads(path.read_text(encoding="utf-8")))
        metas.sort(key=lambda m: m["trained_at"], reverse=True)
        active = self.active_version
        for meta in metas:
            meta["is_active"] = meta["version"] == active
        return metas
