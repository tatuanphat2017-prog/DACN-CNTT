namespace DACN.Api.Entities;

public class Student
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int EnrollmentYear { get; set; }

    public User User { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public ICollection<Enrollment> Enrollments { get; set; } = [];
}
