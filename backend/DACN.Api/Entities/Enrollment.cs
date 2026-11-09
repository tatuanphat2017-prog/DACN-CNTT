namespace DACN.Api.Entities;

public class Enrollment
{
    public int Id { get; set; }
    public int CourseSectionId { get; set; }
    public int StudentId { get; set; }

    // GPA tích lũy chụp lại lúc đăng ký (đặc trưng ML, tránh rò rỉ dữ liệu)
    public decimal? PriorGpa { get; set; }

    public decimal? AttendanceScore { get; set; }   // backend tự tính từ điểm danh
    public decimal? AssignmentScore { get; set; }
    public decimal? MidtermScore { get; set; }
    public decimal? FinalScore { get; set; }
    public decimal? TotalScore { get; set; }        // backend tự tính
    public bool? IsPassed { get; set; }             // backend tự tính, nhãn ML

    public CourseSection CourseSection { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = [];
    public ICollection<RiskPrediction> RiskPredictions { get; set; } = [];
    public ICollection<Warning> Warnings { get; set; } = [];
}
