using System.ComponentModel.DataAnnotations;
using DACN.Api.Entities;

namespace DACN.Api.Dtos;

public record StudentProfile(string StudentCode, int DepartmentId, string ClassName, int EnrollmentYear);

public record LecturerProfile(string LecturerCode, int DepartmentId);

public record UserListItem(int Id, string Username, string FullName, string? Email, UserRole Role, bool IsActive, DateTime CreatedAt);

public record UserDetail(
    int Id, string Username, string FullName, string? Email, UserRole Role, bool IsActive, DateTime CreatedAt,
    StudentProfile? Student, LecturerProfile? Lecturer);

/// <summary>Role = Student thì bắt buộc có Student; Role = Lecturer thì bắt buộc có Lecturer.</summary>
public record CreateUserRequest(
    [Required, MaxLength(50)] string Username,
    [Required, MinLength(8), MaxLength(100)] string Password,
    [Required, MaxLength(100)] string FullName,
    [EmailAddress, MaxLength(100)] string? Email,
    UserRole Role,
    StudentProfile? Student,
    LecturerProfile? Lecturer);

public record UpdateUserRequest(
    [Required, MaxLength(100)] string FullName,
    [EmailAddress, MaxLength(100)] string? Email,
    bool IsActive,
    StudentProfile? Student,
    LecturerProfile? Lecturer);

public record ResetPasswordRequest([Required, MinLength(8), MaxLength(100)] string NewPassword);

public record StudentItem(int Id, string StudentCode, string FullName, string ClassName, string DepartmentName);

public record LecturerItem(int Id, string LecturerCode, string FullName, string DepartmentName);
