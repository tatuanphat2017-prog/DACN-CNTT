using System.ComponentModel.DataAnnotations;
using DACN.Api.Entities;

namespace DACN.Api.Dtos;

/// <summary>Tóm tắt một buổi đã điểm danh. Date = null nếu buổi chưa diễn ra.</summary>
public record AttendanceSessionSummary(int SessionNo, DateOnly? Date, int Present, int Absent, int Late, int Excused);

public record AttendanceEntry(int EnrollmentId, int StudentId, string StudentCode, string FullName, AttendanceStatus Status);

public record AttendanceSheet(int SectionId, int SessionNo, DateOnly Date, List<AttendanceEntry> Entries);

public record AttendanceMark(int EnrollmentId, AttendanceStatus Status);

public record SaveAttendanceRequest(DateOnly Date, [Required, MinLength(1)] List<AttendanceMark> Marks);
