using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Dự đoán nguy cơ rớt môn bằng Machine Learning. Admin và Lecturer xem, chỉ Lecturer dạy lớp đó được yêu cầu dự đoán.</summary>
[ApiController]
[Route("api")]
public class RiskController(AppDbContext db, SectionAccessService access, RiskService risk) : ApiControllerBase
{
    /// <summary>
    /// Chạy dự đoán cho cả lớp (gọi ML service, lưu kết quả, giữ lịch sử). Sinh viên ra mức High được hệ thống tự gửi cảnh báo
    /// (không gửi lại nếu đã có cảnh báo tự động trong 7 ngày). Trả về tổng hợp số sinh viên theo từng mức rủi ro.
    /// Trả 400 nếu lớp chưa có sinh viên hoặc chưa điểm danh buổi nào; 503 nếu ML service không khả dụng.
    /// </summary>
    [HttpPost("sections/{sectionId:int}/predict")]
    [Authorize(Roles = Roles.Lecturer)]
    [ProducesResponseType<RiskSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> Predict(int sectionId) =>
        RunAsync(async () =>
        {
            var result = await access.GetAsync(sectionId);
            if (Reject(result) is { } denied)
                return denied;
            return Ok(await risk.PredictSectionAsync(result.Section!));
        });

    /// <summary>Tổng hợp số sinh viên theo mức rủi ro của lớp (dùng vẽ biểu đồ dashboard), tính trên kết quả dự đoán mới nhất của từng sinh viên.</summary>
    [HttpGet("sections/{sectionId:int}/risk-summary")]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<RiskSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Summary(int sectionId)
    {
        if (Reject(await access.GetAsync(sectionId)) is { } denied)
            return denied;
        return Ok(await risk.SummaryAsync(sectionId));
    }

    /// <summary>
    /// Kết quả dự đoán mới nhất của từng sinh viên trong lớp, lọc theo mức rủi ro, sắp xếp xác suất giảm dần.
    /// Lớp chưa từng dự đoán thì trả danh sách rỗng (gọi POST predict trước).
    /// </summary>
    [HttpGet("sections/{sectionId:int}/risks")]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<IReadOnlyList<RiskItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Risks(int sectionId, [FromQuery] RiskLevel? level)
    {
        if (Reject(await access.GetAsync(sectionId)) is { } denied)
            return denied;

        var query = risk.Latest().AsNoTracking().Where(p => p.Enrollment.CourseSectionId == sectionId);
        if (level is not null)
        {
            query = query.Where(p => p.RiskLevel == level);
        }

        var rows = await query.OrderByDescending(p => p.Probability).ThenBy(p => p.Enrollment.Student.StudentCode)
            .Select(p => new
            {
                p.EnrollmentId, p.Enrollment.StudentId, p.Enrollment.Student.StudentCode, p.Enrollment.Student.User.FullName,
                p.Probability, p.RiskLevel, p.TopFactors, p.PredictedAt,
            })
            .ToListAsync();

        return Ok(rows.Select(r => new RiskItem(r.EnrollmentId, r.StudentId, r.StudentCode, r.FullName,
            r.Probability, r.RiskLevel, RiskService.ParseFactors(r.TopFactors), r.PredictedAt)).ToList());
    }

    /// <summary>Lịch sử dự đoán của một sinh viên trong lớp (theo EnrollmentId), cũ trước mới sau, dùng vẽ biểu đồ xu hướng.</summary>
    [HttpGet("enrollments/{enrollmentId:int}/risk-history")]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<IReadOnlyList<RiskHistoryPoint>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(int enrollmentId)
    {
        var sectionId = await db.Enrollments.Where(e => e.Id == enrollmentId).Select(e => (int?)e.CourseSectionId).FirstOrDefaultAsync();
        if (sectionId is null)
            return NotFound();
        if (Reject(await access.GetAsync(sectionId.Value)) is { } denied)
            return denied;

        return Ok(await db.RiskPredictions.AsNoTracking().Where(p => p.EnrollmentId == enrollmentId)
            .OrderBy(p => p.PredictedAt).ThenBy(p => p.Id)
            .Select(p => new RiskHistoryPoint(p.PredictedAt, p.Probability, p.RiskLevel))
            .ToListAsync());
    }
}
