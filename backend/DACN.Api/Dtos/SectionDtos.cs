using System.ComponentModel.DataAnnotations;

namespace DACN.Api.Dtos;

/// <summary>Trọng số các điểm thành phần, tổng 4 giá trị phải bằng 1.</summary>
public record SectionWeights(decimal AttendanceWeight, decimal AssignmentWeight, decimal MidtermWeight, decimal FinalWeight);

public record SectionListItem(
    int Id, string Code, int CourseId, string CourseCode, string CourseName,
    int SemesterId, string SemesterName, int LecturerId, string LecturerName,
    int TotalSessions, int StudentCount);

public record SectionDetail(
    int Id, string Code, int CourseId, string CourseCode, string CourseName,
    int SemesterId, string SemesterName, int LecturerId, string LecturerName,
    int TotalSessions, SectionWeights Weights, int StudentCount, SectionScheduleInfo? Schedule);

/// <summary>
/// Lịch học hằng tuần và lịch thi của lớp. Nhóm lịch học (Weekday, StartPeriod, PeriodCount, RoomId) phải điền đủ cả bốn hoặc bỏ trống cả bốn.
/// Weekday 2..8 = Thứ hai..Chủ nhật; tiết 1..12 (tiết 1 bắt đầu 07:00, mỗi tiết 45 phút). Lịch thi: ExamDate bắt buộc nếu có giờ hoặc phòng thi.
/// </summary>
public record SectionScheduleRequest(
    int? Weekday, int? StartPeriod, int? PeriodCount, int? RoomId,
    DateOnly? ExamDate, TimeOnly? ExamStartTime, int? ExamRoomId);

public record SectionScheduleInfo(
    int? Weekday, int? StartPeriod, int? PeriodCount, TimeOnly? StartTime, TimeOnly? EndTime,
    int? RoomId, string? RoomCode, string? Building, int? Floor,
    DateOnly? ExamDate, TimeOnly? ExamStartTime, int? ExamRoomId, string? ExamRoomCode, string? ExamBuilding, int? ExamFloor);

public record UpsertSectionRequest(
    [Required, MaxLength(30)] string Code,
    int CourseId,
    int SemesterId,
    int LecturerId,
    [Range(1, 100)] int TotalSessions,
    SectionWeights Weights,
    SectionScheduleRequest? Schedule = null);

public record SectionStudent(int EnrollmentId, int StudentId, string StudentCode, string FullName, string ClassName);

public record EnrollStudentsRequest([Required, MinLength(1)] List<int> StudentIds);

public record EnrollStudentsResult(int Enrolled, int AlreadyEnrolled);
