namespace DACN.Api.Entities;

public class RiskPrediction
{
    public int Id { get; set; }
    public int EnrollmentId { get; set; }
    public int ModelVersionId { get; set; }
    public double Probability { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public string TopFactors { get; set; } = "[]";   // JSON các yếu tố chính
    public DateTime PredictedAt { get; set; } = DateTime.UtcNow;

    public Enrollment Enrollment { get; set; } = null!;
    public ModelVersion ModelVersion { get; set; } = null!;
}
