using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Thống kê tổng quan cho Admin: điểm, GPA, tỷ lệ đạt, phân bố rủi ro theo năm học và học kỳ.</summary>
[ApiController]
[Route("api/stats")]
[Authorize(Roles = Roles.Admin)]
public class StatsController(AppDbContext db, RiskService risk) : ApiControllerBase
{
    private record Row(
        int SemesterId, int StudentId, string ClassName, int CourseSectionId, int CourseId, string CourseCode, string CourseName,
        int Credits, decimal? TotalScore, bool? IsPassed, RiskLevel? Risk);

    /// <summary>
    /// Xu hướng qua các năm học: mỗi năm có số liệu gộp và danh sách học kỳ (điểm trung bình, GPA trung bình của sinh viên, tỷ lệ đạt).
    /// Năm học mới nhất trước. Chỉ các đăng ký đã có kết quả cuối mới được tính điểm, GPA và tỷ lệ đạt.
    /// </summary>
    [HttpGet("trend")]
    [ProducesResponseType<StatsTrend>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Trend()
    {
        var semesters = await db.Semesters.AsNoTracking().OrderBy(s => s.StartDate).ToListAsync();
        var rows = (await LoadRowsAsync(null)).ToLookup(r => r.SemesterId);
        var sectionCounts = await db.CourseSections.AsNoTracking().GroupBy(s => s.SemesterId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

        var stats = semesters.Select(s => Summarize(s, rows[s.Id].ToList(), sectionCounts.GetValueOrDefault(s.Id))).ToList();

        var years = stats.GroupBy(s => s.AcademicYear).OrderByDescending(g => g.Key).Select(g =>
        {
            var yearRows = g.SelectMany(s => rows[s.SemesterId]).ToList();
            var graded = yearRows.Where(r => r.IsPassed is not null).ToList();
            return new YearStats(g.Key, yearRows.Select(r => r.StudentId).Distinct().Count(), graded.Count,
                AverageScore(graded), AverageGpa(graded), PassRate(graded),
                g.OrderByDescending(s => s.SemesterId).ToList());
        }).ToList();
        return Ok(new StatsTrend(years));
    }

    /// <summary>
    /// Tổng quan chi tiết một học kỳ: số liệu chung, phân bố điểm tổng, phân bố xếp loại GPA của sinh viên, phân bố mức rủi ro (theo dự đoán mới nhất),
    /// thống kê theo môn (điểm trung bình, tỷ lệ đạt, số sinh viên rủi ro) và theo lớp sinh hoạt. Không truyền semesterId thì lấy học kỳ hiện tại
    /// (không có thì lấy học kỳ mới nhất). Học kỳ không tồn tại trả 404.
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType<SemesterOverview>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Overview([FromQuery] int? semesterId)
    {
        var semester = semesterId is not null
            ? await db.Semesters.AsNoTracking().FirstOrDefaultAsync(s => s.Id == semesterId)
            : await db.Semesters.AsNoTracking().OrderByDescending(s => s.IsCurrent).ThenByDescending(s => s.StartDate).FirstOrDefaultAsync();
        if (semester is null)
            return NotFound();

        var rows = await LoadRowsAsync(semester.Id);
        var sectionCount = await db.CourseSections.CountAsync(s => s.SemesterId == semester.Id);
        var graded = rows.Where(r => r.IsPassed is not null).ToList();

        var scoreBands = Bands(graded.Where(r => r.TotalScore is not null).Select(r => r.TotalScore!.Value),
            ("Dưới 4.0", 0m, 4m), ("4.0 – 5.4", 4m, 5.5m), ("5.5 – 6.9", 5.5m, 7m), ("7.0 – 8.4", 7m, 8.5m), ("8.5 – 10", 8.5m, 10.01m));
        var gpaBands = Bands(StudentGpas(graded).Select(x => x.Gpa ?? 0m),
            ("Yếu (dưới 2.0)", 0m, 2m), ("Trung bình (2.0 – 2.49)", 2m, 2.5m), ("Khá (2.5 – 3.19)", 2.5m, 3.2m),
            ("Giỏi (3.2 – 3.59)", 3.2m, 3.6m), ("Xuất sắc (3.6 – 4.0)", 3.6m, 4.01m));

        var predicted = rows.Count(r => r.Risk is not null);
        var distribution = new RiskDistribution(rows.Count(r => r.Risk == RiskLevel.Low), rows.Count(r => r.Risk == RiskLevel.Medium),
            rows.Count(r => r.Risk == RiskLevel.High), rows.Count - predicted);

        var warnings = await db.Warnings.CountAsync(w => w.Enrollment.CourseSection.SemesterId == semester.Id);

        var courses = rows.GroupBy(r => new { r.CourseId, r.CourseCode, r.CourseName, r.Credits }).OrderBy(g => g.Key.CourseCode)
            .Select(g =>
            {
                var gradedCourse = g.Where(r => r.IsPassed is not null).ToList();
                return new CourseStats(g.Key.CourseId, g.Key.CourseCode, g.Key.CourseName, g.Key.Credits, gradedCourse.Count,
                    AverageScore(gradedCourse), PassRate(gradedCourse),
                    g.Count(r => r.Risk == RiskLevel.High), g.Count(r => r.Risk == RiskLevel.Medium), g.Count(r => r.Risk == RiskLevel.Low));
            }).ToList();

        var classes = rows.GroupBy(r => r.ClassName).OrderBy(g => g.Key).Select(g =>
        {
            var gradedClass = g.Where(r => r.IsPassed is not null).ToList();
            return new ClassStats(g.Key, g.Select(r => r.StudentId).Distinct().Count(), AverageGpa(gradedClass), PassRate(gradedClass));
        }).ToList();

        return Ok(new SemesterOverview(Summarize(semester, rows, sectionCount), scoreBands, gpaBands, distribution, warnings, courses, classes));
    }

    /// <summary>Đọc các đăng ký (toàn bộ hoặc của một học kỳ) kèm mức rủi ro mới nhất.</summary>
    private async Task<List<Row>> LoadRowsAsync(int? semesterId)
    {
        var query = db.Enrollments.AsNoTracking();
        if (semesterId is not null)
            query = query.Where(e => e.CourseSection.SemesterId == semesterId);

        var rows = await query.Select(e => new
        {
            e.CourseSection.SemesterId, e.StudentId, e.Student.ClassName, e.CourseSectionId, e.CourseSection.CourseId,
            CourseCode = e.CourseSection.Course.Code, CourseName = e.CourseSection.Course.Name, e.CourseSection.Course.Credits,
            e.TotalScore, e.IsPassed, e.Id,
        }).ToListAsync();

        var riskQuery = risk.Latest().AsNoTracking();
        if (semesterId is not null)
            riskQuery = riskQuery.Where(p => p.Enrollment.CourseSection.SemesterId == semesterId);
        var levels = await riskQuery.Select(p => new { p.EnrollmentId, p.RiskLevel }).ToDictionaryAsync(x => x.EnrollmentId, x => x.RiskLevel);

        return rows.Select(r => new Row(r.SemesterId, r.StudentId, r.ClassName, r.CourseSectionId, r.CourseId, r.CourseCode, r.CourseName,
            r.Credits, r.TotalScore, r.IsPassed, levels.TryGetValue(r.Id, out var level) ? level : null)).ToList();
    }

    private static SemesterStats Summarize(Semester s, List<Row> rows, int sections)
    {
        var graded = rows.Where(r => r.IsPassed is not null).ToList();
        return new SemesterStats(s.Id, s.Name, Timetable.AcademicYear(s.StartDate), s.IsCurrent, sections,
            rows.Select(r => r.StudentId).Distinct().Count(), rows.Count, graded.Count,
            AverageScore(graded), AverageGpa(graded), PassRate(graded));
    }

    private static decimal? AverageScore(List<Row> graded)
    {
        var scores = graded.Where(r => r.TotalScore is not null).Select(r => r.TotalScore!.Value).ToList();
        return scores.Count == 0 ? null : Math.Round(scores.Average(), 2, MidpointRounding.AwayFromZero);
    }

    private static double? PassRate(List<Row> graded) =>
        graded.Count == 0 ? null : Math.Round(graded.Count(r => r.IsPassed == true) / (double)graded.Count, 4);

    /// <summary>GPA học kỳ của từng sinh viên (nhóm theo sinh viên và học kỳ, trọng số tín chỉ).</summary>
    private static List<(int StudentId, decimal? Gpa)> StudentGpas(List<Row> graded) =>
        graded.GroupBy(r => (r.StudentId, r.SemesterId))
            .Select(g => (g.Key.StudentId, GradeScale.Gpa(g.Select(r => (r.TotalScore ?? 0m, r.IsPassed == true, r.Credits)))))
            .ToList();

    /// <summary>GPA trung bình của các sinh viên (mỗi sinh viên một giá trị cho mỗi học kỳ).</summary>
    private static decimal? AverageGpa(List<Row> graded)
    {
        var gpas = StudentGpas(graded).Where(x => x.Gpa is not null).Select(x => x.Gpa!.Value).ToList();
        return gpas.Count == 0 ? null : Math.Round(gpas.Average(), 2, MidpointRounding.AwayFromZero);
    }

    private static List<ScoreBand> Bands(IEnumerable<decimal> values, params (string Label, decimal From, decimal To)[] bands)
    {
        var list = values.ToList();
        return bands.Select(b => new ScoreBand(b.Label, list.Count(v => v >= b.From && v < b.To))).ToList();
    }
}
