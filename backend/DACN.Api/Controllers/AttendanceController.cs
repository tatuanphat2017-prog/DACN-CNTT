using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Điểm danh theo buổi của một lớp học phần. Admin và Lecturer xem, chỉ Lecturer dạy lớp đó được ghi.</summary>
[ApiController]
[Route("api/sections/{sectionId:int}/attendance")]
public class AttendanceController(AppDbContext db, SectionAccessService access, GradingService grading) : ApiControllerBase
{
    /// <summary>Tóm tắt tất cả các buổi của lớp (đủ TotalSessions buổi, buổi chưa điểm danh có Date = null).</summary>
    [HttpGet]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<IReadOnlyList<AttendanceSessionSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sessions(int sectionId)
    {
        var result = await access.GetAsync(sectionId);
        if (Reject(result) is { } denied)
            return denied;

        var held = await db.AttendanceSessions.AsNoTracking().Where(s => s.CourseSectionId == sectionId)
            .Select(s => new
            {
                s.SessionNo,
                s.Date,
                Present = s.AttendanceRecords.Count(r => r.Status == AttendanceStatus.Present),
                Absent = s.AttendanceRecords.Count(r => r.Status == AttendanceStatus.Absent),
                Late = s.AttendanceRecords.Count(r => r.Status == AttendanceStatus.Late),
                Excused = s.AttendanceRecords.Count(r => r.Status == AttendanceStatus.Excused),
            })
            .ToDictionaryAsync(s => s.SessionNo);

        return Ok(Enumerable.Range(1, result.Section!.TotalSessions)
            .Select(n => held.TryGetValue(n, out var s)
                ? new AttendanceSessionSummary(n, s.Date, s.Present, s.Absent, s.Late, s.Excused)
                : new AttendanceSessionSummary(n, null, 0, 0, 0, 0))
            .ToList());
    }

    /// <summary>Bảng điểm danh của một buổi: mỗi sinh viên một trạng thái (Present, Absent, Late, Excused). Buổi chưa điểm danh mặc định tất cả Present.</summary>
    [HttpGet("{sessionNo:int}")]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<AttendanceSheet>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sheet(int sectionId, int sessionNo)
    {
        var result = await access.GetAsync(sectionId);
        if (Reject(result) is { } denied)
            return denied;
        if (sessionNo < 1 || sessionNo > result.Section!.TotalSessions)
            return NotFound();
        return Ok(await BuildSheetAsync(sectionId, sessionNo));
    }

    /// <summary>
    /// Lưu điểm danh một buổi (tạo mới hoặc ghi đè). Sinh viên không có trong Marks được ghi mặc định Present.
    /// Backend tự tính lại AttendanceScore, TotalScore, IsPassed của cả lớp. Trả 400 nếu có EnrollmentId không thuộc lớp
    /// hoặc trùng nhau; 404 nếu số buổi ngoài khoảng 1 đến TotalSessions.
    /// </summary>
    [HttpPut("{sessionNo:int}")]
    [Authorize(Roles = Roles.Lecturer)]
    [ProducesResponseType<AttendanceSheet>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Save(int sectionId, int sessionNo, SaveAttendanceRequest request)
    {
        var result = await access.GetAsync(sectionId);
        if (Reject(result) is { } denied)
            return denied;
        var section = result.Section!;
        if (sessionNo < 1 || sessionNo > section.TotalSessions)
            return NotFound();

        if (request.Marks.GroupBy(m => m.EnrollmentId).Any(g => g.Count() > 1))
            return InvalidField("Marks", "Một sinh viên xuất hiện nhiều lần trong danh sách điểm danh");
        var enrollmentIds = await db.Enrollments.Where(e => e.CourseSectionId == sectionId).Select(e => e.Id).ToListAsync();
        var invalid = request.Marks.Select(m => m.EnrollmentId).Except(enrollmentIds).ToList();
        if (invalid.Count > 0)
            return InvalidField("Marks", $"EnrollmentId không thuộc lớp này: {string.Join(", ", invalid)}");

        var requested = request.Marks.ToDictionary(m => m.EnrollmentId, m => m.Status);
        await db.InTransactionAsync(async () =>
        {
            var session = await db.AttendanceSessions.FirstOrDefaultAsync(s => s.CourseSectionId == sectionId && s.SessionNo == sessionNo);
            if (session is null)
            {
                session = new AttendanceSession { CourseSectionId = sectionId, SessionNo = sessionNo, Date = request.Date };
                db.AttendanceSessions.Add(session);
            }
            else
            {
                session.Date = request.Date;
            }
            await db.SaveChangesAsync();

            var existing = await db.AttendanceRecords.Where(r => r.AttendanceSessionId == session.Id).ToDictionaryAsync(r => r.EnrollmentId);
            foreach (var enrollmentId in enrollmentIds)
            {
                if (existing.TryGetValue(enrollmentId, out var record))
                {
                    if (requested.TryGetValue(enrollmentId, out var status))
                        record.Status = status;
                }
                else
                {
                    db.AttendanceRecords.Add(new AttendanceRecord
                    {
                        AttendanceSessionId = session.Id,
                        EnrollmentId = enrollmentId,
                        Status = requested.GetValueOrDefault(enrollmentId, AttendanceStatus.Present),
                    });
                }
            }
            await db.SaveChangesAsync();
            await grading.RecalculateSectionAsync(section);
            return true;
        });
        return Ok(await BuildSheetAsync(sectionId, sessionNo));
    }

    private async Task<AttendanceSheet> BuildSheetAsync(int sectionId, int sessionNo)
    {
        var session = await db.AttendanceSessions.AsNoTracking().FirstOrDefaultAsync(s => s.CourseSectionId == sectionId && s.SessionNo == sessionNo);
        var statuses = session is null
            ? new Dictionary<int, AttendanceStatus>()
            : await db.AttendanceRecords.AsNoTracking().Where(r => r.AttendanceSessionId == session.Id).ToDictionaryAsync(r => r.EnrollmentId, r => r.Status);

        var students = await db.Enrollments.AsNoTracking().Where(e => e.CourseSectionId == sectionId)
            .OrderBy(e => e.Student.StudentCode)
            .Select(e => new { e.Id, e.StudentId, e.Student.StudentCode, e.Student.User.FullName })
            .ToListAsync();

        var entries = students
            .Select(s => new AttendanceEntry(s.Id, s.StudentId, s.StudentCode, s.FullName, statuses.GetValueOrDefault(s.Id, AttendanceStatus.Present)))
            .ToList();
        return new AttendanceSheet(sectionId, sessionNo, session?.Date ?? DateOnly.FromDateTime(DateTime.UtcNow), entries);
    }
}
