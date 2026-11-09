from typing import Any, Literal

from pydantic import BaseModel, Field


class Features(BaseModel):
    absent_rate: float = Field(ge=0, le=1, description="Tỷ lệ buổi vắng (0-1)")
    late_rate: float = Field(ge=0, le=1, description="Tỷ lệ buổi đi trễ (0-1)")
    assignment_score: float | None = Field(default=None, ge=0, le=10, description="Điểm bài tập (thang 10), null nếu chưa có")
    midterm_score: float | None = Field(default=None, ge=0, le=10, description="Điểm giữa kỳ (thang 10), null nếu chưa có")
    prior_gpa: float | None = Field(default=None, ge=0, le=4, description="GPA tích lũy thang 4 trước học kỳ, null nếu là sinh viên mới")


class PredictItem(Features):
    id: int = Field(description="Mã định danh do bên gọi đặt (backend dùng EnrollmentId), trả lại nguyên vẹn")


class PredictRequest(BaseModel):
    items: list[PredictItem] = Field(min_length=1, max_length=500)


class Factor(BaseModel):
    feature: str
    impact: float = Field(description="Mức thay đổi xác suất rớt (0-1) do đặc trưng này")
    direction: Literal["increase", "decrease"]


class Prediction(BaseModel):
    id: int
    probability: float = Field(description="Xác suất rớt môn (0-1)")
    risk_level: Literal["Low", "Medium", "High"]
    factors: list[Factor]


class PredictResponse(BaseModel):
    model_version: str
    predictions: list[Prediction]


class TrainRow(Features):
    failed: bool


class TrainRequest(BaseModel):
    rows: list[TrainRow] = Field(default_factory=list, description="Dữ liệu huấn luyện từ backend")
    include_base: bool = Field(default=True, description="Gộp thêm bộ dữ liệu nền đóng gói sẵn trong service")
    activate: Literal["auto", "always", "never"] = Field(
        default="auto", description="auto: dùng ngay nếu ROC-AUC không kém mô hình hiện tại quá 0.01")


class TrainResponse(BaseModel):
    model: dict[str, Any]
    activated: bool
    previous_version: str | None


class ActivateRequest(BaseModel):
    version: str
