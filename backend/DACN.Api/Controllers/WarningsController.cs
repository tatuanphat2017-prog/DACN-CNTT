using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Cảnh báo gửi cho sinh viên có nguy cơ rớt môn (do giảng viên gửi hoặc hệ thống tự gửi khi rủi ro Cao).</summary>
[ApiController]
[Route("api")]
public class WarningsController(AppDbContext db, ICurrentUser user, SectionAccessService access) : ApiControllerBase
{
    /// <summary>Giảng viên gửi cảnh báo cho một sinh viên (theo EnrollmentId trong lớp mình dạy). Nội dung tối đa 1000 ký tự.</summary>
    [HttpPost("warnings")]
    [Authorize(Roles = Roles.Lecturer)]
    [ProducesResponseType<WarningItem>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Send(SendWarningRequest request)
    {
        var message = request.Message.Trim();
        if (message.Length == 0)
            return InvalidField("Message", "Nội dung cảnh báo không được để trống");

        var enrollment = await db.Enrollments.Where(e => e.Id == request.EnrollmentId).Select(e => new { e.Id, e.CourseSectionId }).FirstOrDefaultAsync();
        if (enrollment is null)
            return NotFound();
        if (Reject(await access.GetAsync(enrollment.CourseSectionId)) is { } denied)
            return denied;

        var warning = new Warning { EnrollmentId = enrollment.Id, LecturerId = await user.GetLecturerIdAsync(), Message = message };
        db.Warnings.Add(warning);
        await db.SaveChangesAsync();

        var item = await Items(db.Warnings.AsNoTracking().Where(w => w.Id == warning.Id)).FirstAsync();
        return Created($"/api/warnings/{warning.Id}", item);
    }

    /// <summary>Các cảnh báo trong một lớp (của giảng viên và của hệ thống), mới nhất trước.</summary>
    [HttpGet("sections/{sectionId:int}/warnings")]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<IReadOnlyList<WarningItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BySection(int sectionId)
    {
        if (Reject(await access.GetAsync(sectionId)) is { } denied)
            return denied;
        return Ok(await Items(db.Warnings.AsNoTracking().Where(w => w.Enrollment.CourseSectionId == sectionId)).ToListAsync());
    }

    /// <summary>Cảnh báo của chính sinh viên đang đăng nhập, mới nhất trước.</summary>
    [HttpGet("warnings/me")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType<IReadOnlyList<WarningItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine()
    {
        var studentId = await user.GetStudentIdAsync();
        return Ok(await Items(db.Warnings.AsNoTracking().Where(w => w.Enrollment.StudentId == studentId)).ToListAsync());
    }

    /// <summary>Số cảnh báo chưa đọc của sinh viên (hiện trên chuông thông báo).</summary>
    [HttpGet("warnings/me/unread-count")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType<UnreadCount>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UnreadCount()
    {
        var studentId = await user.GetStudentIdAsync();
        return Ok(new UnreadCount(await db.Warnings.CountAsync(w => w.Enrollment.StudentId == studentId && !w.IsRead)));
    }

    /// <summary>Sinh viên đánh dấu một cảnh báo của mình là đã đọc (gọi lại nhiều lần không sao). Cảnh báo của người khác trả 404.</summary>
    [HttpPut("warnings/{id:int}/read")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(int id)
    {
        var studentId = await user.GetStudentIdAsync();
        var warning = await db.Warnings.FirstOrDefaultAsync(w => w.Id == id && w.Enrollment.StudentId == studentId);
        if (warning is null)
            return NotFound();

        warning.IsRead = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static IQueryable<WarningItem> Items(IQueryable<Warning> query) =>
        query.OrderByDescending(w => w.CreatedAt).ThenByDescending(w => w.Id)
            .Select(w => new WarningItem(w.Id, w.EnrollmentId, w.Enrollment.CourseSectionId, w.Enrollment.CourseSection.Course.Name,
                w.Enrollment.Student.User.FullName, w.Lecturer == null ? null : w.Lecturer.User.FullName, w.Message, w.IsRead, w.CreatedAt));
}
