using DACN.Api.Entities;

namespace DACN.Api.Dtos;

/// <summary>
/// Một yếu tố ảnh hưởng tới rủi ro. Impact là mức ảnh hưởng (0–1, càng lớn càng quan trọng).
/// Direction = "increase" nếu yếu tố làm tăng nguy cơ rớt, "decrease" nếu làm giảm.
/// </summary>
public record RiskFactor(string Feature, string Label, double Impact, string Direction);

/// <summary>Probability là xác suất rớt (0–1). Low &lt; 0.3, Medium 0.3–0.6, High &gt; 0.6.</summary>
public record RiskItem(
    int EnrollmentId, int StudentId, string StudentCode, string FullName,
    double Probability, RiskLevel RiskLevel, List<RiskFactor> TopFactors, DateTime PredictedAt);

public record RiskSummary(int SectionId, int Total, int Low, int Medium, int High, DateTime? LastPredictedAt, string? ModelVersion);

public record RiskHistoryPoint(DateTime PredictedAt, double Probability, RiskLevel RiskLevel);
