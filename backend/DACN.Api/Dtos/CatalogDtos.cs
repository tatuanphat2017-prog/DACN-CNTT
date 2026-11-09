using System.ComponentModel.DataAnnotations;

namespace DACN.Api.Dtos;

public record DepartmentDto(int Id, string Code, string Name);

public record UpsertDepartmentRequest([Required, MaxLength(20)] string Code, [Required, MaxLength(100)] string Name);

public record CourseDto(int Id, string Code, string Name, int Credits, int DepartmentId, string DepartmentName);

public record UpsertCourseRequest(
    [Required, MaxLength(20)] string Code,
    [Required, MaxLength(150)] string Name,
    [Range(1, 20)] int Credits,
    int DepartmentId);

public record SemesterDto(int Id, string Name, DateOnly StartDate, DateOnly EndDate, bool IsCurrent);

/// <summary>Đặt IsCurrent = true sẽ tự bỏ cờ "học kỳ hiện tại" của học kỳ khác.</summary>
public record UpsertSemesterRequest([Required, MaxLength(50)] string Name, DateOnly StartDate, DateOnly EndDate, bool IsCurrent);
