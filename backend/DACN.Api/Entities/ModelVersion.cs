namespace DACN.Api.Entities;

public class ModelVersion
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1 { get; set; }
    public double RocAuc { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DateTime TrainedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; }

    public ICollection<RiskPrediction> RiskPredictions { get; set; } = [];
}
