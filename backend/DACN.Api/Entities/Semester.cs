namespace DACN.Api.Entities;

public class Semester
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; set; }

    public ICollection<CourseSection> CourseSections { get; set; } = [];
}
