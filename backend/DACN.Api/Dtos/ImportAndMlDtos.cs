namespace DACN.Api.Dtos;

public record ImportError(int Row, string Message);

public record ImportResult(int TotalRows, int Created, int Skipped, List<ImportError> Errors);

public record ModelVersionItem(
    int Id, string Version, string Algorithm,
    double Accuracy, double Precision, double Recall, double F1, double RocAuc,
    DateTime TrainedAt, bool IsActive);

public record RetrainResponse(string Status, string Message);
