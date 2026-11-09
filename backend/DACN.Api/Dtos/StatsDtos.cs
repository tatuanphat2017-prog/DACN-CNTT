namespace DACN.Api.Dtos;

/// <summary>Số liệu tổng hợp của một học kỳ. Chỉ tính các đăng ký đã có kết quả cuối (IsPassed) cho điểm, GPA và tỷ lệ đạt.</summary>
public record SemesterStats(
    int SemesterId, string SemesterName, string AcademicYear, bool IsCurrent,
    int Sections, int Students, int Enrollments, int Graded,
    decimal? AverageScore, decimal? AverageGpa, double? PassRate);

public record YearStats(string AcademicYear, int Students, int Graded, decimal? AverageScore, decimal? AverageGpa, double? PassRate, List<SemesterStats> Semesters);

/// <summary>Xu hướng theo thời gian: từng năm học, trong mỗi năm có từng học kỳ (năm mới nhất trước).</summary>
public record StatsTrend(List<YearStats> Years);

public record ScoreBand(string Label, int Count);

public record CourseStats(int CourseId, string CourseCode, string CourseName, int Credits, int Graded, decimal? AverageScore, double? PassRate, int High, int Medium, int Low);

public record ClassStats(string ClassName, int Students, decimal? AverageGpa, double? PassRate);

public record RiskDistribution(int Low, int Medium, int High, int NotPredicted);

/// <summary>Tổng quan chi tiết một học kỳ: số liệu chung, phân bố điểm, phân bố rủi ro, thống kê theo môn và theo lớp sinh hoạt.</summary>
public record SemesterOverview(
    SemesterStats Summary, List<ScoreBand> ScoreDistribution, List<ScoreBand> GpaDistribution, RiskDistribution Risk,
    int Warnings, List<CourseStats> Courses, List<ClassStats> Classes);
