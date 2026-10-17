namespace DACN.Api.Entities;

public class AttendanceRecord
{
    public int Id { get; set; }
    public int AttendanceSessionId { get; set; }
    public int EnrollmentId { get; set; }
    public AttendanceStatus Status { get; set; }

    public AttendanceSession AttendanceSession { get; set; } = null!;
    public Enrollment Enrollment { get; set; } = null!;
}
