namespace DACN.Api.Entities;

public class Course
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Credits { get; set; }
    public int DepartmentId { get; set; }

    public Department Department { get; set; } = null!;
    public ICollection<CourseSection> CourseSections { get; set; } = [];
}
