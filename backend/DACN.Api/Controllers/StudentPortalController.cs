using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Cổng sinh viên: dữ liệu của chính người đang đăng nhập. Chỉ Student.</summary>
[ApiController]
[Route("api/students/me")]
[Authorize(Roles = Roles.Student)]
public class StudentPortalController(AppDbContext db, ICurrentUser user, RiskService risk) : ApiControllerBase
{
    /// <summary>Các lớp sinh viên đang học (mới nhất trước), kèm số buổi vắng, điểm tổng và mức rủi ro hiện tại.</summary>
    [HttpGet("sections")]
    [ProducesResponseType<IReadOnlyList<MySectionItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Sections()
    {
        if (await user.GetStudentIdAsync() is not { } studentId)
            return Forbid();

        return Ok(await db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.CourseSection.Semester.StartDate).ThenBy(e => e.CourseSection.Code)
            .Select(e => new MySectionItem(e.CourseSectionId, e.CourseSection.Code, e.CourseSection.Course.Name,
                e.CourseSection.Semester.Name, e.CourseSection.Lecturer.User.FullName,
                e.AttendanceRecords.Count(r => r.Status == AttendanceStatus.Absent), e.CourseSection.TotalSessions, e.TotalScore,
                e.RiskPredictions.OrderByDescending(p => p.PredictedAt).Select(p => (RiskLevel?)p.RiskLevel).FirstOrDefault()))
            .ToListAsync());
    }

    /// <summary>Chi tiết một lớp của sinh viên: các điểm thành phần, trọng số, lịch sử điểm danh. Lớp không đăng ký trả 404.</summary>
    [HttpGet("sections/{sectionId:int}")]
    [ProducesResponseType<MySectionDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Section(int sectionId)
    {
        if (await user.GetStudentIdAsync() is not { } studentId)
            return Forbid();

        var detail = await db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId && e.CourseSectionId == sectionId)
            .Select(e => new
            {
                e.Id,
                Item = new MySectionItem(e.CourseSectionId, e.CourseSection.Code, e.CourseSection.Course.Name,
                    e.CourseSection.Semester.Name, e.CourseSection.Lecturer.User.FullName,
                    e.AttendanceRecords.Count(r => r.Status == AttendanceStatus.Absent), e.CourseSection.TotalSessions, e.TotalScore,
                    e.RiskPredictions.OrderByDescending(p => p.PredictedAt).Select(p => (RiskLevel?)p.RiskLevel).FirstOrDefault()),
                Weights = new SectionWeights(e.CourseSection.AttendanceWeight, e.CourseSection.AssignmentWeight,
                    e.CourseSection.MidtermWeight, e.CourseSection.FinalWeight),
                e.AttendanceScore, e.AssignmentScore, e.MidtermScore, e.FinalScore, e.TotalScore, e.IsPassed,
            })
            .FirstOrDefaultAsync();
        if (detail is null)
            return NotFound();

        var attendance = await db.AttendanceRecords.AsNoTracking().Where(r => r.EnrollmentId == detail.Id)
            .OrderBy(r => r.AttendanceSession.SessionNo)
            .Select(r => new MyAttendanceRecord(r.AttendanceSession.SessionNo, r.AttendanceSession.Date, r.Status))
            .ToListAsync();
        return Ok(new MySectionDetail(detail.Item, detail.Weights, detail.AttendanceScore, detail.AssignmentScore,
            detail.MidtermScore, detail.FinalScore, detail.TotalScore, detail.IsPassed, attendance));
    }

    /// <summary>
    /// Mức rủi ro của sinh viên ở từng lớp (kết quả dự đoán mới nhất), kèm các yếu tố chính và gợi ý cải thiện. Xác suất cao trước.
    /// Lớp chưa được dự đoán thì không có trong danh sách.
    /// </summary>
    [HttpGet("risks")]
    [ProducesResponseType<IReadOnlyList<MyRisk>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Risks()
    {
        if (await user.GetStudentIdAsync() is not { } studentId)
            return Forbid();

        var rows = await risk.Latest().AsNoTracking().Where(p => p.Enrollment.StudentId == studentId)
            .OrderByDescending(p => p.Probability)
            .Select(p => new
            {
                p.Enrollment.CourseSectionId, p.Enrollment.CourseSection.Code, p.Enrollment.CourseSection.Course.Name,
                p.Probability, p.RiskLevel, p.TopFactors, p.PredictedAt,
            })
            .ToListAsync();

        return Ok(rows.Select(r =>
        {
            var factors = RiskService.ParseFactors(r.TopFactors);
            return new MyRisk(r.CourseSectionId, r.Code, r.Name, r.Probability, r.RiskLevel, factors, RiskText.Suggestions(r.RiskLevel, factors), r.PredictedAt);
        }).ToList());
    }
}
