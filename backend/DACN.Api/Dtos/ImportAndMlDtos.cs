namespace DACN.Api.Dtos;

public record ImportError(int Row, string Message);

public record ImportResult(int TotalRows, int Created, int Skipped, List<ImportError> Errors);

public record ModelVersionItem(
    int Id, string Version, string Algorithm,
    double Accuracy, double Precision, double Recall, double F1, double RocAuc,
    DateTime TrainedAt, bool IsActive);

public record RetrainResponse(string Status, string Message);

/// <summary>Trạng thái lần huấn luyện lại gần nhất. Status: Idle (chưa chạy), Running, Succeeded, Failed.</summary>
public record RetrainStatusDto(string Status, string? Message, DateTime? StartedAt, DateTime? FinishedAt, string? Version, bool? Activated);
