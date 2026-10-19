using System.ComponentModel.DataAnnotations;

namespace DACN.Api.Dtos;

/// <summary>
/// Một dòng bảng điểm. AttendanceScore, TotalScore, IsPassed do backend tự tính (chỉ đọc).
/// TotalScore và IsPassed là null cho tới khi có FinalScore.
/// </summary>
public record GradeRow(
    int EnrollmentId, int StudentId, string StudentCode, string FullName,
    int AbsentCount, decimal? PriorGpa,
    decimal? AttendanceScore, decimal? AssignmentScore, decimal? MidtermScore, decimal? FinalScore,
    decimal? TotalScore, bool? IsPassed);

public record GradeSheet(int SectionId, SectionWeights Weights, int TotalSessions, List<GradeRow> Rows);

/// <summary>Chỉ gửi các cột giảng viên được nhập. Điểm trong khoảng 0–10, null = giữ nguyên/chưa có.</summary>
public record SaveGradeRow(
    int EnrollmentId,
    [Range(typeof(decimal), "0", "10")] decimal? AssignmentScore,
    [Range(typeof(decimal), "0", "10")] decimal? MidtermScore,
    [Range(typeof(decimal), "0", "10")] decimal? FinalScore);

public record SaveGradesRequest([Required, MinLength(1)] List<SaveGradeRow> Rows);
