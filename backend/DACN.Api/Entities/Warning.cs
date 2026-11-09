namespace DACN.Api.Entities;

public class Warning
{
    public int Id { get; set; }
    public int EnrollmentId { get; set; }
    public int? LecturerId { get; set; }   // null = hệ thống tự gửi
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Enrollment Enrollment { get; set; } = null!;
    public Lecturer? Lecturer { get; set; }
}
