from fastapi import FastAPI

app = FastAPI(title="DACN ML Service")


@app.get("/health")
def health():
    return {"status": "ok"}
