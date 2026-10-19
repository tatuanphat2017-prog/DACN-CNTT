using System.ComponentModel.DataAnnotations;

namespace DACN.Api.Dtos;

/// <summary>SenderName = null nghĩa là hệ thống tự gửi (khi dự đoán ra mức High).</summary>
public record WarningItem(
    int Id, int EnrollmentId, int SectionId, string CourseName, string StudentName,
    string? SenderName, string Message, bool IsRead, DateTime CreatedAt);

public record SendWarningRequest(int EnrollmentId, [Required, MaxLength(1000)] string Message);

public record UnreadCount(int Count);
