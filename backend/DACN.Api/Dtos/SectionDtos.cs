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
    int TotalSessions, SectionWeights Weights, int StudentCount);

public record UpsertSectionRequest(
    [Required, MaxLength(30)] string Code,
    int CourseId,
    int SemesterId,
    int LecturerId,
    [Range(1, 100)] int TotalSessions,
    SectionWeights Weights);

public record SectionStudent(int EnrollmentId, int StudentId, string StudentCode, string FullName, string ClassName);

public record EnrollStudentsRequest([Required, MinLength(1)] List<int> StudentIds);

public record EnrollStudentsResult(int Enrolled, int AlreadyEnrolled);
