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

/// <summary>Một buổi học hằng tuần trong thời khóa biểu: môn, giảng viên, thứ, tiết, giờ và phòng (tòa, lầu).</summary>
public record TimetableItem(
    int SectionId, string SectionCode, string CourseCode, string CourseName, string LecturerName,
    int Weekday, int StartPeriod, int PeriodCount, TimeOnly StartTime, TimeOnly EndTime,
    string RoomCode, string Building, int Floor);

public record MyTimetable(int SemesterId, string SemesterName, string AcademicYear, List<TimetableItem> Items);

/// <summary>Một buổi học cụ thể theo ngày (lịch học). SessionNo là buổi thứ mấy của lớp trong học kỳ.</summary>
public record ScheduleItem(
    DateOnly Date, int Weekday, int SessionNo,
    int SectionId, string SectionCode, string CourseName, string LecturerName,
    TimeOnly StartTime, TimeOnly EndTime, string RoomCode, string Building, int Floor);

/// <summary>Điểm thi cuối kỳ và lịch thi của một lớp. Chưa có điểm thi hoặc chưa xếp lịch thi thì các trường tương ứng là null.</summary>
public record ExamItem(
    int SectionId, string SectionCode, string CourseName, int Credits, string SemesterName, string AcademicYear,
    DateOnly? ExamDate, TimeOnly? ExamStartTime, string? RoomCode, string? Building, int? Floor,
    decimal? FinalScore, decimal? TotalScore, bool? IsPassed);

/// <summary>Một môn trong bảng điểm. Result: "Đạt", "Không đạt" hoặc "Đang học".</summary>
public record TranscriptCourse(
    int SectionId, string SectionCode, string CourseCode, string CourseName, int Credits,
    decimal? AttendanceScore, decimal? AssignmentScore, decimal? MidtermScore, decimal? FinalScore, decimal? TotalScore,
    decimal? GradePoint, string Result);

/// <summary>Bảng điểm một học kỳ. Gpa là GPA học kỳ (thang 4, trọng số tín chỉ); null nếu học kỳ chưa có môn nào có kết quả.</summary>
public record TranscriptSemester(
    int SemesterId, string SemesterName, string AcademicYear, bool IsFinished,
    decimal? Gpa, int CreditsRegistered, int CreditsPassed, List<TranscriptCourse> Courses);

public record TranscriptYear(string AcademicYear, decimal? Gpa, int CreditsPassed);

/// <summary>Bảng điểm toàn khóa: GPA tích lũy, tín chỉ tích lũy, GPA từng năm học và từng học kỳ (mới nhất trước).</summary>
public record Transcript(
    string StudentCode, string FullName, decimal? CumulativeGpa, int CreditsPassed,
    List<TranscriptYear> Years, List<TranscriptSemester> Semesters);
