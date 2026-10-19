using DACN.Api.Auth;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Mock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DACN.Api.Controllers;

/// <summary>Dự đoán nguy cơ rớt môn bằng Machine Learning. Admin và Lecturer xem, chỉ Lecturer yêu cầu dự đoán.</summary>
[ApiController]
[Route("api")]
[Mock]
public class RiskController : ControllerBase
{
    /// <summary>
    /// Chạy dự đoán cho cả lớp (gọi ML service, lưu kết quả). Sinh viên ra mức High sẽ được hệ thống tự gửi cảnh báo.
    /// Trả về tổng hợp số sinh viên theo từng mức rủi ro. Trả 503 nếu ML service không khả dụng.
    /// </summary>
    [HttpPost("sections/{sectionId:int}/predict")]
    [Authorize(Roles = Roles.Lecturer)]
    [ProducesResponseType<RiskSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Predict(int sectionId) =>
        MockData.Section(sectionId) is null ? NotFound() : Ok(MockData.RiskSummary(sectionId));

    /// <summary>Tổng hợp số sinh viên theo mức rủi ro của lớp (dùng vẽ biểu đồ dashboard).</summary>
    [HttpGet("sections/{sectionId:int}/risk-summary")]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<RiskSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Summary(int sectionId) =>
        MockData.Section(sectionId) is null ? NotFound() : Ok(MockData.RiskSummary(sectionId));

    /// <summary>Kết quả dự đoán mới nhất của từng sinh viên trong lớp, lọc theo mức rủi ro, sắp xếp xác suất giảm dần.</summary>
    [HttpGet("sections/{sectionId:int}/risks")]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<IReadOnlyList<RiskItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Risks(int sectionId, [FromQuery] RiskLevel? level)
    {
        if (MockData.Section(sectionId) is null) return NotFound();
        var items = MockData.Risks().AsEnumerable();
        if (level is not null) items = items.Where(r => r.RiskLevel == level);
        return Ok(items.OrderByDescending(r => r.Probability).ToList());
    }

    /// <summary>Lịch sử dự đoán của một sinh viên trong lớp (theo EnrollmentId), dùng vẽ biểu đồ xu hướng.</summary>
    [HttpGet("enrollments/{enrollmentId:int}/risk-history")]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<IReadOnlyList<RiskHistoryPoint>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult History(int enrollmentId) =>
        MockData.Risks().Any(r => r.EnrollmentId == enrollmentId) ? Ok(MockData.RiskHistory(enrollmentId)) : NotFound();
}

/// <summary>Cảnh báo gửi cho sinh viên có nguy cơ rớt môn.</summary>
[ApiController]
[Route("api")]
[Mock]
public class WarningsController : ControllerBase
{
    /// <summary>Giảng viên gửi cảnh báo cho một sinh viên (theo EnrollmentId trong lớp mình dạy).</summary>
    [HttpPost("warnings")]
    [Authorize(Roles = Roles.Lecturer)]
    [ProducesResponseType<WarningItem>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Send(SendWarningRequest request)
    {
        var student = MockData.SectionStudents().FirstOrDefault(s => s.EnrollmentId == request.EnrollmentId);
        if (student is null) return NotFound();
        var section = MockData.Sections[0];
        return Created("/api/warnings/100", new WarningItem(100, request.EnrollmentId, section.Id, section.CourseName,
            student.FullName, section.LecturerName, request.Message, false, DateTime.UtcNow));
    }

    /// <summary>Các cảnh báo đã gửi trong một lớp, mới nhất trước.</summary>
    [HttpGet("sections/{sectionId:int}/warnings")]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<IReadOnlyList<WarningItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult BySection(int sectionId) =>
        MockData.Section(sectionId) is null ? NotFound() : Ok(MockData.Warnings(false));

    /// <summary>Cảnh báo của chính sinh viên đang đăng nhập, mới nhất trước.</summary>
    [HttpGet("warnings/me")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType<IReadOnlyList<WarningItem>>(StatusCodes.Status200OK)]
    public IActionResult Mine() => Ok(MockData.Warnings(true));

    /// <summary>Số cảnh báo chưa đọc của sinh viên (hiện trên chuông thông báo).</summary>
    [HttpGet("warnings/me/unread-count")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType<UnreadCount>(StatusCodes.Status200OK)]
    public IActionResult UnreadCount() => Ok(new UnreadCount(MockData.Warnings(true).Count(w => !w.IsRead)));

    /// <summary>Sinh viên đánh dấu một cảnh báo đã đọc.</summary>
    [HttpPut("warnings/{id:int}/read")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult MarkRead(int id) => MockData.Warnings(true).Any(w => w.Id == id) ? NoContent() : NotFound();
}

/// <summary>Cổng sinh viên: dữ liệu của chính người đang đăng nhập. Chỉ Student.</summary>
[ApiController]
[Route("api/students/me")]
[Authorize(Roles = Roles.Student)]
[Mock]
public class StudentPortalController : ControllerBase
{
    /// <summary>Các lớp sinh viên đang học, kèm số buổi vắng, điểm tổng và mức rủi ro hiện tại.</summary>
    [HttpGet("sections")]
    [ProducesResponseType<IReadOnlyList<MySectionItem>>(StatusCodes.Status200OK)]
    public IActionResult Sections() => Ok(MockData.MySections());

    /// <summary>Chi tiết một lớp của sinh viên: các điểm thành phần, trọng số, lịch sử điểm danh.</summary>
    [HttpGet("sections/{sectionId:int}")]
    [ProducesResponseType<MySectionDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Section(int sectionId) => MockData.MySection(sectionId) is { } detail ? Ok(detail) : NotFound();

    /// <summary>Mức rủi ro của sinh viên ở từng lớp, kèm các yếu tố chính và gợi ý cải thiện.</summary>
    [HttpGet("risks")]
    [ProducesResponseType<IReadOnlyList<MyRisk>>(StatusCodes.Status200OK)]
    public IActionResult Risks() => Ok(MockData.MyRisks());
}

/// <summary>Quản lý mô hình Machine Learning. Chỉ Admin.</summary>
[ApiController]
[Route("api/ml")]
[Authorize(Roles = Roles.Admin)]
[Mock]
public class MlController : ControllerBase
{
    /// <summary>Danh sách các phiên bản mô hình và độ chính xác (Accuracy, Precision, Recall, F1, ROC-AUC).</summary>
    [HttpGet("models")]
    [ProducesResponseType<IReadOnlyList<ModelVersionItem>>(StatusCodes.Status200OK)]
    public IActionResult Models() => Ok(MockData.Models);

    /// <summary>Yêu cầu huấn luyện lại mô hình. Chạy nền, trả 202 ngay; xem kết quả ở GET /api/ml/models.</summary>
    [HttpPost("retrain")]
    [ProducesResponseType<RetrainResponse>(StatusCodes.Status202Accepted)]
    public IActionResult Retrain() => Accepted(new RetrainResponse("Started", "Đã bắt đầu huấn luyện lại mô hình"));
}
