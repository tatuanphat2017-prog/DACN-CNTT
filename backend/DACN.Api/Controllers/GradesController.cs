using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Bảng điểm của một lớp học phần. Admin và Lecturer xem, chỉ Lecturer dạy lớp đó được nhập.</summary>
[ApiController]
[Route("api/sections/{sectionId:int}/grades")]
public class GradesController(AppDbContext db, SectionAccessService access, GradingService grading) : ApiControllerBase
{
    /// <summary>Bảng điểm cả lớp. AttendanceScore, TotalScore, IsPassed do backend tính, chỉ đọc.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<GradeSheet>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int sectionId)
    {
        var result = await access.GetAsync(sectionId);
        if (Reject(result) is { } denied)
            return denied;
        return Ok(await BuildSheetAsync(result.Section!));
    }

    /// <summary>
    /// Lưu điểm bài tập, giữa kỳ, cuối kỳ cho nhiều sinh viên cùng lúc. Điểm trong khoảng 0–10 (sai trả 400); điểm null nghĩa là giữ nguyên.
    /// Có đủ điểm thì backend tự tính TotalScore và IsPassed. Trả 400 nếu EnrollmentId không thuộc lớp hoặc trùng nhau.
    /// Trả về bảng điểm đã cập nhật.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Roles.Lecturer)]
    [ProducesResponseType<GradeSheet>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Save(int sectionId, SaveGradesRequest request)
    {
        var result = await access.GetAsync(sectionId);
        if (Reject(result) is { } denied)
            return denied;
        var section = result.Section!;

        if (request.Rows.GroupBy(r => r.EnrollmentId).Any(g => g.Count() > 1))
            return InvalidField("Rows", "Một sinh viên xuất hiện nhiều lần trong bảng điểm");
        var ids = request.Rows.Select(r => r.EnrollmentId).ToList();
        var enrollments = await db.Enrollments.Where(e => e.CourseSectionId == sectionId && ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id);
        var invalid = ids.Except(enrollments.Keys).ToList();
        if (invalid.Count > 0)
            return InvalidField("Rows", $"EnrollmentId không thuộc lớp này: {string.Join(", ", invalid)}");

        foreach (var row in request.Rows)
        {
            var enrollment = enrollments[row.EnrollmentId];
            enrollment.AssignmentScore = row.AssignmentScore ?? enrollment.AssignmentScore;
            enrollment.MidtermScore = row.MidtermScore ?? enrollment.MidtermScore;
            enrollment.FinalScore = row.FinalScore ?? enrollment.FinalScore;
        }
        await db.SaveChangesAsync();
        await grading.RecalculateSectionAsync(section);
        return Ok(await BuildSheetAsync(section));
    }

    private async Task<GradeSheet> BuildSheetAsync(CourseSection section)
    {
        var rows = await db.Enrollments.AsNoTracking().Where(e => e.CourseSectionId == section.Id)
            .OrderBy(e => e.Student.StudentCode)
            .Select(e => new GradeRow(e.Id, e.StudentId, e.Student.StudentCode, e.Student.User.FullName,
                e.AttendanceRecords.Count(r => r.Status == AttendanceStatus.Absent), e.PriorGpa,
                e.AttendanceScore, e.AssignmentScore, e.MidtermScore, e.FinalScore, e.TotalScore, e.IsPassed))
            .ToListAsync();
        var weights = new SectionWeights(section.AttendanceWeight, section.AssignmentWeight, section.MidtermWeight, section.FinalWeight);
        return new GradeSheet(section.Id, weights, section.TotalSessions, rows);
    }
}
