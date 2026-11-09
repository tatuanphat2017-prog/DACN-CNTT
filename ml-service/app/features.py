"""Đặc trưng và ngưỡng mức rủi ro, dùng chung cho huấn luyện và dự đoán."""

# 5 đặc trưng đầu vào (cùng thứ tự ở mọi nơi). Chụp ở giữa kỳ, đúng thời điểm cảnh báo sớm:
#   absent_rate      tỷ lệ buổi vắng trên số buổi đã điểm danh (0-1)
#   late_rate        tỷ lệ buổi đi trễ (0-1)
#   assignment_score điểm bài tập (thang 10), có thể thiếu
#   midterm_score    điểm giữa kỳ (thang 10), có thể thiếu
#   prior_gpa        GPA tích lũy thang 4 trước học kỳ, thiếu với sinh viên mới
FEATURES = ["absent_rate", "late_rate", "assignment_score", "midterm_score", "prior_gpa"]

# Ràng buộc đơn điệu theo kiến thức nghiệp vụ, đúng thứ tự FEATURES: vắng/đi trễ càng nhiều thì xác suất rớt không thể giảm (+1);
# điểm và GPA càng cao thì xác suất rớt không thể tăng (-1). Giúp mô hình không học các quan hệ nhiễu ngược chiều
# và làm lời giải thích luôn hợp lý (ví dụ không bao giờ có chuyện "GPA thấp làm giảm nguy cơ").
MONOTONIC = [1, 1, -1, -1, -1]

# Nhãn: 1 = rớt môn (lớp dương), 0 = qua môn
TARGET = "failed"

# Mức rủi ro theo xác suất rớt (khớp hợp đồng API): Low < 0.3, Medium 0.3-0.6, High >= 0.6
LOW_MAX = 0.3
HIGH_MIN = 0.6


def risk_level(probability: float) -> str:
    if probability < LOW_MAX:
        return "Low"
    if probability < HIGH_MIN:
        return "Medium"
    return "High"
