namespace DACN.Api.Entities;

/// <summary>Phòng học: thuộc một tòa nhà và một lầu. Dùng cho thời khóa biểu và lịch thi.</summary>
public class Room
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;       // ví dụ A2.301
    public string Building { get; set; } = string.Empty;   // tòa, ví dụ A2
    public int Floor { get; set; }                          // lầu
    public int Capacity { get; set; }

    public ICollection<CourseSection> CourseSections { get; set; } = [];
}
