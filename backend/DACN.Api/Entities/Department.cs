namespace DACN.Api.Entities;

public class Department
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<Student> Students { get; set; } = [];
    public ICollection<Lecturer> Lecturers { get; set; } = [];
    public ICollection<Course> Courses { get; set; } = [];
}
