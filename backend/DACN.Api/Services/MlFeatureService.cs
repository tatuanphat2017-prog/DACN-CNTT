using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Services;

/// <summary>Tạo đặc trưng cho ML từ dữ liệu trong database. Một nơi duy nhất để lúc huấn luyện và lúc dự đoán dùng cùng một cách tính.</summary>
public class MlFeatureService(AppDbContext db)
{
    /// <summary>
    /// Đặc trưng hiện tại của các sinh viên trong lớp (dùng khi dự đoán): tỷ lệ vắng/trễ tính trên các buổi đã điểm danh của từng sinh viên.
    /// </summary>
    public async Task<List<MlFeatures>> CurrentFeaturesAsync(int sectionId)
    {
        var rows = await db.Enrollments.AsNoTracking().Where(e => e.CourseSectionId == sectionId)
            .OrderBy(e => e.Id)
            .Select(e => new
            {
                e.Id, e.AssignmentScore, e.MidtermScore, e.PriorGpa,
                Recorded = e.AttendanceRecords.Count(),
                Absent = e.AttendanceRecords.Count(r => r.Status == AttendanceStatus.Absent),
                Late = e.AttendanceRecords.Count(r => r.Status == AttendanceStatus.Late),
            })
            .ToListAsync();

        return rows.Select(r => new MlFeatures(r.Id, Rate(r.Absent, r.Recorded), Rate(r.Late, r.Recorded),
            ToDouble(r.AssignmentScore), ToDouble(r.MidtermScore), ToDouble(r.PriorGpa))).ToList();
    }

    /// <summary>
    /// Dữ liệu huấn luyện: mọi đăng ký đã có kết quả cuối kỳ (IsPassed khác null). Đặc trưng được "chụp" ở giữa kỳ,
    /// tức chỉ tính các buổi từ 1 đến nửa đầu học kỳ, đúng thời điểm cảnh báo sớm; nhãn là kết quả cuối kỳ (Failed = rớt).
    /// </summary>
    public async Task<List<TrainingRow>> TrainingRowsAsync()
    {
        var rows = await db.Enrollments.AsNoTracking().Where(e => e.IsPassed != null)
            .OrderBy(e => e.Id)
            .Select(e => new
            {
                e.Id, e.AssignmentScore, e.MidtermScore, e.PriorGpa,
                Failed = e.IsPassed == false,
                Recorded = e.AttendanceRecords.Count(r => r.AttendanceSession.SessionNo <= (e.CourseSection.TotalSessions + 1) / 2),
                Absent = e.AttendanceRecords.Count(r => r.AttendanceSession.SessionNo <= (e.CourseSection.TotalSessions + 1) / 2
                                                         && r.Status == AttendanceStatus.Absent),
                Late = e.AttendanceRecords.Count(r => r.AttendanceSession.SessionNo <= (e.CourseSection.TotalSessions + 1) / 2
                                                       && r.Status == AttendanceStatus.Late),
            })
            .ToListAsync();

        return rows.Select(r => new TrainingRow(r.Id, Rate(r.Absent, r.Recorded), Rate(r.Late, r.Recorded),
            ToDouble(r.AssignmentScore), ToDouble(r.MidtermScore), ToDouble(r.PriorGpa), r.Failed)).ToList();
    }

    private static double Rate(int count, int total) => total == 0 ? 0 : Math.Round((double)count / total, 4);

    private static double? ToDouble(decimal? value) => value is null ? null : (double)value.Value;
}
