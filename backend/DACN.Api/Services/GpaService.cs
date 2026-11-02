using DACN.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Services;

/// <summary>Quy đổi điểm 10 sang thang 4 và tính GPA tích lũy (có trọng số theo tín chỉ).</summary>
public static class GradeScale
{
    /// <summary>Thang điểm: 8.5–10 → 4.0, 7.0–8.4 → 3.0, 5.5–6.9 → 2.0, 4.0–5.4 → 1.0, dưới 4.0 → 0.</summary>
    public static decimal ToGradePoint(decimal totalScore) => totalScore switch
    {
        >= 8.5m => 4.0m,
        >= 7.0m => 3.0m,
        >= 5.5m => 2.0m,
        >= 4.0m => 1.0m,
        _ => 0m,
    };

    /// <summary>Môn không đạt (IsPassed = false, kể cả bị cấm thi vì vắng nhiều) tính 0 điểm. Chưa có môn nào → null.</summary>
    public static decimal? Gpa(IEnumerable<(decimal TotalScore, bool Passed, int Credits)> results)
    {
        var list = results.ToList();
        var credits = list.Sum(r => r.Credits);
        if (credits == 0)
        {
            return null;
        }
        var points = list.Sum(r => (r.Passed ? ToGradePoint(r.TotalScore) : 0m) * r.Credits);
        return Math.Round(points / credits, 2);
    }
}

public class GpaService(AppDbContext db)
{
    /// <summary>
    /// GPA tích lũy của sinh viên tại thời điểm trước khi học kỳ bắt đầu: chỉ tính các môn đã có kết quả cuối
    /// ở các học kỳ đã kết thúc. Đây là đặc trưng PriorGpa cho ML (tránh rò rỉ dữ liệu). Chưa có lịch sử → null.
    /// </summary>
    public async Task<decimal?> PriorGpaAsync(int studentId, DateOnly semesterStart)
    {
        var rows = await db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.IsPassed != null && e.TotalScore != null
                        && e.CourseSection.Semester.EndDate <= semesterStart)
            .Select(e => new { e.TotalScore, e.IsPassed, e.CourseSection.Course.Credits })
            .ToListAsync();

        return GradeScale.Gpa(rows.Select(r => (r.TotalScore!.Value, r.IsPassed == true, r.Credits)));
    }
}
