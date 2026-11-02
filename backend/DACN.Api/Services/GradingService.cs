using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Services;

/// <summary>Số buổi theo từng trạng thái của một sinh viên trong một lớp.</summary>
public record AttendanceCounts(int Present, int Late, int Excused, int Absent)
{
    public int Total => Present + Late + Excused + Absent;
}

/// <summary>Các công thức tính điểm (xem docs/ERD.md, mục "Quy tắc tính điểm"). Là hàm thuần nên dễ kiểm thử.</summary>
public static class ScoreCalculator
{
    public const decimal PassScore = 4.0m;
    public const decimal MaxAbsentRatio = 0.20m;

    /// <summary>
    /// Điểm chuyên cần (thang 10) = 10 × (có mặt + 0.5 × đi trễ + có phép) / số buổi đã điểm danh của sinh viên.
    /// Chưa điểm danh buổi nào thì null. Khi điểm danh đủ TotalSessions buổi, đây chính là công thức cuối kỳ.
    /// </summary>
    public static decimal? AttendanceScore(AttendanceCounts counts) =>
        counts.Total == 0 ? null : Math.Round(10m * (counts.Present + 0.5m * counts.Late + counts.Excused) / counts.Total, 2, MidpointRounding.AwayFromZero);

    /// <summary>Điểm tổng = tổng (điểm thành phần × trọng số), làm tròn 1 chữ số theo kiểu "nửa lên" (7.25 → 7.3). Thiếu bất kỳ điểm nào thì null.</summary>
    public static decimal? TotalScore(decimal? attendance, decimal? assignment, decimal? midterm, decimal? final, SectionWeights w)
    {
        if (attendance is null || assignment is null || midterm is null || final is null)
        {
            return null;
        }
        return Math.Round(
            attendance.Value * w.AttendanceWeight + assignment.Value * w.AssignmentWeight
            + midterm.Value * w.MidtermWeight + final.Value * w.FinalWeight, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>Qua môn khi điểm tổng ≥ 4.0 và số buổi vắng (Absent) ≤ 20% tổng số buổi. Chưa có điểm tổng thì null.</summary>
    public static bool? IsPassed(decimal? total, int absentCount, int totalSessions) =>
        total is null ? null : total >= PassScore && absentCount <= MaxAbsentRatio * totalSessions;
}

public class GradingService(AppDbContext db)
{
    /// <summary>Tính lại AttendanceScore, TotalScore, IsPassed cho mọi sinh viên của lớp. Gọi sau khi lưu điểm danh hoặc điểm.</summary>
    public async Task RecalculateSectionAsync(CourseSection section)
    {
        var enrollments = await db.Enrollments.Where(e => e.CourseSectionId == section.Id).ToListAsync();
        var grouped = await db.AttendanceRecords
            .Where(r => r.AttendanceSession.CourseSectionId == section.Id)
            .GroupBy(r => new { r.EnrollmentId, r.Status })
            .Select(g => new { g.Key.EnrollmentId, g.Key.Status, Count = g.Count() })
            .ToListAsync();
        var weights = new SectionWeights(section.AttendanceWeight, section.AssignmentWeight, section.MidtermWeight, section.FinalWeight);

        foreach (var enrollment in enrollments)
        {
            var mine = grouped.Where(g => g.EnrollmentId == enrollment.Id).ToList();
            int Count(AttendanceStatus status) => mine.Where(g => g.Status == status).Sum(g => g.Count);
            var counts = new AttendanceCounts(Count(AttendanceStatus.Present), Count(AttendanceStatus.Late),
                Count(AttendanceStatus.Excused), Count(AttendanceStatus.Absent));

            enrollment.AttendanceScore = ScoreCalculator.AttendanceScore(counts);
            enrollment.TotalScore = ScoreCalculator.TotalScore(enrollment.AttendanceScore, enrollment.AssignmentScore,
                enrollment.MidtermScore, enrollment.FinalScore, weights);
            enrollment.IsPassed = ScoreCalculator.IsPassed(enrollment.TotalScore, counts.Absent, section.TotalSessions);
        }
        await db.SaveChangesAsync();
    }
}
