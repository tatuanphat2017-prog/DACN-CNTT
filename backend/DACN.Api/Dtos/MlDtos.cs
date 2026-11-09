namespace DACN.Api.Dtos;

/// <summary>
/// Đặc trưng của một sinh viên trong một lớp, dùng làm đầu vào cho mô hình ML.
/// AbsentRate, LateRate: tỷ lệ buổi vắng / đi trễ trên số buổi đã điểm danh (0–1).
/// AssignmentScore, MidtermScore: điểm thang 10, null nếu chưa có. PriorGpa: GPA tích lũy thang 4 trước học kỳ, null nếu là sinh viên mới.
/// </summary>
public record MlFeatures(int EnrollmentId, double AbsentRate, double LateRate, double? AssignmentScore, double? MidtermScore, double? PriorGpa);

/// <summary>Một dòng dữ liệu huấn luyện: đặc trưng chụp ở giữa kỳ và nhãn Failed (true = cuối kỳ rớt môn).</summary>
public record TrainingRow(
    int EnrollmentId, double AbsentRate, double LateRate, double? AssignmentScore, double? MidtermScore, double? PriorGpa, bool Failed);
