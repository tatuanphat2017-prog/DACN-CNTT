namespace DACN.Api.Entities;

public class Lecturer
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string LecturerCode { get; set; } = string.Empty;
    public int DepartmentId { get; set; }

    public User User { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public ICollection<CourseSection> CourseSections { get; set; } = [];
    public ICollection<Warning> Warnings { get; set; } = [];
}
