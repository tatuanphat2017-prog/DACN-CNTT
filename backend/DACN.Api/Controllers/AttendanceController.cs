using DACN.Api.Auth;
using DACN.Api.Dtos;
using DACN.Api.Mock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DACN.Api.Controllers;

/// <summary>Điểm danh theo buổi của một lớp học phần. Admin và Lecturer xem, chỉ Lecturer ghi.</summary>
[ApiController]
[Route("api/sections/{sectionId:int}/attendance")]
[Mock]
public class AttendanceController : ControllerBase
{
    /// <summary>Tóm tắt tất cả các buổi của lớp (đủ TotalSessions buổi, buổi chưa diễn ra có Date = null).</summary>
    [HttpGet]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<IReadOnlyList<AttendanceSessionSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Sessions(int sectionId) =>
        MockData.Section(sectionId) is null ? NotFound() : Ok(MockData.AttendanceSessions());

    /// <summary>Bảng điểm danh của một buổi: mỗi sinh viên một trạng thái (Present, Absent, Late, Excused).</summary>
    [HttpGet("{sessionNo:int}")]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<AttendanceSheet>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Sheet(int sectionId, int sessionNo) =>
        MockData.Section(sectionId) is null || sessionNo is < 1 or > 15 ? NotFound() : Ok(MockData.AttendanceSheet(sectionId, sessionNo));

    /// <summary>
    /// Lưu điểm danh một buổi (tạo mới hoặc ghi đè). Backend tự tính lại AttendanceScore, TotalScore, IsPassed của lớp.
    /// Trả 400 nếu có EnrollmentId không thuộc lớp.
    /// </summary>
    [HttpPut("{sessionNo:int}")]
    [Authorize(Roles = Roles.Lecturer)]
    [ProducesResponseType<AttendanceSheet>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Save(int sectionId, int sessionNo, SaveAttendanceRequest request)
    {
        if (MockData.Section(sectionId) is null || sessionNo is < 1 or > 15) return NotFound();
        var sheet = MockData.AttendanceSheet(sectionId, sessionNo);
        var marks = request.Marks.ToDictionary(m => m.EnrollmentId, m => m.Status);
        return Ok(sheet with
        {
            Date = request.Date,
            Entries = [.. sheet.Entries.Select(e => marks.TryGetValue(e.EnrollmentId, out var status) ? e with { Status = status } : e)],
        });
    }
}

/// <summary>Bảng điểm của một lớp học phần. Admin và Lecturer xem, chỉ Lecturer nhập.</summary>
[ApiController]
[Route("api/sections/{sectionId:int}/grades")]
[Mock]
public class GradesController : ControllerBase
{
    /// <summary>Bảng điểm cả lớp. AttendanceScore, TotalScore, IsPassed do backend tính, chỉ đọc.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<GradeSheet>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Get(int sectionId) => MockData.Section(sectionId) is null ? NotFound() : Ok(MockData.Grades(sectionId));

    /// <summary>
    /// Lưu điểm bài tập, giữa kỳ, cuối kỳ cho nhiều sinh viên cùng lúc. Điểm phải trong khoảng 0–10 (sai trả 400).
    /// Có FinalScore thì backend tự tính TotalScore và IsPassed. Trả về bảng điểm đã cập nhật.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Roles.Lecturer)]
    [ProducesResponseType<GradeSheet>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Save(int sectionId, SaveGradesRequest request)
    {
        if (MockData.Section(sectionId) is null) return NotFound();
        var sheet = MockData.Grades(sectionId);
        var updates = request.Rows.ToDictionary(r => r.EnrollmentId);
        return Ok(sheet with
        {
            Rows = [.. sheet.Rows.Select(row => updates.TryGetValue(row.EnrollmentId, out var u)
                ? row with
                {
                    AssignmentScore = u.AssignmentScore ?? row.AssignmentScore,
                    MidtermScore = u.MidtermScore ?? row.MidtermScore,
                    FinalScore = u.FinalScore ?? row.FinalScore,
                }
                : row)],
        });
    }
}
