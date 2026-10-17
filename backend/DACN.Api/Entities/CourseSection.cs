namespace DACN.Api.Entities;

public class CourseSection
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public int SemesterId { get; set; }
    public int LecturerId { get; set; }
    public int TotalSessions { get; set; }

    // Trọng số các điểm thành phần, tổng bằng 1
    public decimal AttendanceWeight { get; set; } = 0.10m;
    public decimal AssignmentWeight { get; set; } = 0.20m;
    public decimal MidtermWeight { get; set; } = 0.20m;
    public decimal FinalWeight { get; set; } = 0.50m;

    public Course Course { get; set; } = null!;
    public Semester Semester { get; set; } = null!;
    public Lecturer Lecturer { get; set; } = null!;
    public ICollection<Enrollment> Enrollments { get; set; } = [];
    public ICollection<AttendanceSession> AttendanceSessions { get; set; } = [];
}
