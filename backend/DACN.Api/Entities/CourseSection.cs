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

    // Lịch học hằng tuần (cả nhóm null nghĩa là chưa xếp lịch): Weekday 2..8 = Thứ 2..Chủ nhật, tiết 1..12
    public int? Weekday { get; set; }
    public int? StartPeriod { get; set; }
    public int? PeriodCount { get; set; }
    public int? RoomId { get; set; }

    // Lịch thi cuối kỳ (không bắt buộc)
    public DateOnly? ExamDate { get; set; }
    public TimeOnly? ExamStartTime { get; set; }
    public int? ExamRoomId { get; set; }

    public Course Course { get; set; } = null!;
    public Semester Semester { get; set; } = null!;
    public Lecturer Lecturer { get; set; } = null!;
    public Room? Room { get; set; }
    public Room? ExamRoom { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; } = [];
    public ICollection<AttendanceSession> AttendanceSessions { get; set; } = [];
}
