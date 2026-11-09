using DACN.Api.Entities;

namespace DACN.Api.Dtos;

public record MySectionItem(
    int SectionId, string SectionCode, string CourseName, string SemesterName, string LecturerName,
    int AbsentCount, int TotalSessions, decimal? TotalScore, RiskLevel? RiskLevel);

public record MyAttendanceRecord(int SessionNo, DateOnly Date, AttendanceStatus Status);

public record MySectionDetail(
    MySectionItem Section, SectionWeights Weights,
    decimal? AttendanceScore, decimal? AssignmentScore, decimal? MidtermScore, decimal? FinalScore,
    decimal? TotalScore, bool? IsPassed, List<MyAttendanceRecord> Attendance);

/// <summary>Rủi ro của chính sinh viên trong một lớp, kèm gợi ý cải thiện.</summary>
public record MyRisk(
    int SectionId, string SectionCode, string CourseName,
    double Probability, RiskLevel RiskLevel, List<RiskFactor> TopFactors, List<string> Suggestions, DateTime PredictedAt);
