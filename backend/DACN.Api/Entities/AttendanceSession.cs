namespace DACN.Api.Entities;

public class AttendanceSession
{
    public int Id { get; set; }
    public int CourseSectionId { get; set; }
    public int SessionNo { get; set; }
    public DateOnly Date { get; set; }

    public CourseSection CourseSection { get; set; } = null!;
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = [];
}
