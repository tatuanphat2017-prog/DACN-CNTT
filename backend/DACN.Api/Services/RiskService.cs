using System.Text.Json;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Services;

/// <summary>Lỗi nghiệp vụ có mã HTTP kèm theo (controller chuyển thành ProblemDetails).</summary>
public class ServiceException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public class RiskService(AppDbContext db, MlFeatureService features, IMlClient ml, MlModelService models)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private const int AutoWarningCooldownDays = 7;

    /// <summary>Dự đoán mới nhất của từng đăng ký (mỗi lần dự đoán thêm dòng mới nên lịch sử được giữ nguyên).</summary>
    public IQueryable<RiskPrediction> Latest() =>
        db.RiskPredictions.Where(p => p.Id == db.RiskPredictions
            .Where(x => x.EnrollmentId == p.EnrollmentId)
            .OrderByDescending(x => x.PredictedAt).ThenByDescending(x => x.Id)
            .Select(x => x.Id).FirstOrDefault());

    public static List<RiskFactor> ParseFactors(string json) =>
        JsonSerializer.Deserialize<List<RiskFactor>>(json, Web) ?? [];

    /// <summary>
    /// Dự đoán cho cả lớp: lấy đặc trưng hiện tại, gọi ML service, lưu kết quả (giữ lịch sử), tự gửi cảnh báo cho sinh viên mức Cao.
    /// </summary>
    public async Task<RiskSummary> PredictSectionAsync(CourseSection section)
    {
        var current = await features.CurrentFeaturesAsync(section.Id);
        if (current.Count == 0)
            throw new ServiceException(400, "Lớp chưa có sinh viên");
        if (!await db.AttendanceSessions.AnyAsync(s => s.CourseSectionId == section.Id))
            throw new ServiceException(400, "Lớp chưa có buổi điểm danh nào nên chưa đủ dữ liệu để dự đoán");

        var response = await ml.PredictAsync(
            [.. current.Select(f => new MlPredictItem(f.EnrollmentId, f.AbsentRate, f.LateRate, f.AssignmentScore, f.MidtermScore, f.PriorGpa))]);
        var model = await models.EnsureVersionAsync(response.ModelVersion);

        var byEnrollment = current.ToDictionary(f => f.EnrollmentId);
        var courseName = await db.Courses.Where(c => c.Id == section.CourseId).Select(c => c.Name).FirstAsync();
        var now = DateTime.UtcNow;
        var saved = new List<(RiskPrediction Prediction, List<RiskFactor> Factors)>();

        foreach (var p in response.Predictions.Where(p => byEnrollment.ContainsKey(p.Id)))
        {
            var f = byEnrollment[p.Id];
            var factors = p.Factors.Select(x => new RiskFactor(x.Feature, RiskText.Label(x.Feature, f), x.Impact, x.Direction)).ToList();
            saved.Add((new RiskPrediction
            {
                EnrollmentId = p.Id, ModelVersionId = model.Id, Probability = p.Probability,
                RiskLevel = Enum.Parse<RiskLevel>(p.RiskLevel), TopFactors = JsonSerializer.Serialize(factors, Web), PredictedAt = now,
            }, factors));
        }

        db.RiskPredictions.AddRange(saved.Select(s => s.Prediction));
        await AddAutoWarningsAsync(saved, courseName, now);
        await db.SaveChangesAsync();
        return await SummaryAsync(section.Id);
    }

    /// <summary>Sinh viên mức Cao nhận một cảnh báo do hệ thống gửi (LecturerId = null); không gửi lại nếu đã có cảnh báo tự động trong 7 ngày.</summary>
    private async Task AddAutoWarningsAsync(List<(RiskPrediction Prediction, List<RiskFactor> Factors)> saved, string courseName, DateTime now)
    {
        var high = saved.Where(s => s.Prediction.RiskLevel == RiskLevel.High).ToList();
        if (high.Count == 0)
        {
            return;
        }

        var ids = high.Select(s => s.Prediction.EnrollmentId).ToList();
        var since = now.AddDays(-AutoWarningCooldownDays);
        var recent = (await db.Warnings.Where(w => w.LecturerId == null && ids.Contains(w.EnrollmentId) && w.CreatedAt >= since)
            .Select(w => w.EnrollmentId).Distinct().ToListAsync()).ToHashSet();

        foreach (var (prediction, factors) in high.Where(s => !recent.Contains(s.Prediction.EnrollmentId)))
        {
            db.Warnings.Add(new Warning
            {
                EnrollmentId = prediction.EnrollmentId, LecturerId = null, CreatedAt = now,
                Message = RiskText.AutoWarning(courseName, prediction.Probability, factors),
            });
        }
    }

    public async Task<RiskSummary> SummaryAsync(int sectionId)
    {
        var latest = await Latest().AsNoTracking().Where(p => p.Enrollment.CourseSectionId == sectionId)
            .Select(p => new { p.RiskLevel, p.PredictedAt, p.ModelVersion.Version })
            .ToListAsync();

        var newest = latest.OrderByDescending(x => x.PredictedAt).FirstOrDefault();
        return new RiskSummary(sectionId, latest.Count,
            latest.Count(x => x.RiskLevel == RiskLevel.Low), latest.Count(x => x.RiskLevel == RiskLevel.Medium),
            latest.Count(x => x.RiskLevel == RiskLevel.High), newest?.PredictedAt, newest?.Version);
    }
}
