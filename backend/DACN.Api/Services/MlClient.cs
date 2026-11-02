using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DACN.Api.Services;

// ----- Dữ liệu trao đổi với ML service (JSON snake_case, xem ml-service/app/schemas.py) -----

public record MlPredictItem(int Id, double AbsentRate, double LateRate, double? AssignmentScore, double? MidtermScore, double? PriorGpa);

public record MlFactor(string Feature, double Impact, string Direction);

public record MlPrediction(int Id, double Probability, string RiskLevel, List<MlFactor> Factors);

public record MlPredictResponse(string ModelVersion, List<MlPrediction> Predictions);

public record MlMetrics(double Accuracy, double Precision, double Recall, double F1, double RocAuc, double Brier, double RecallMediumPlus);

public record MlModelInfo(string Version, string Algorithm, DateTimeOffset TrainedAt, int NRows, int NFailed, MlMetrics Metrics, bool IsActive);

public record MlTrainRow(double AbsentRate, double LateRate, double? AssignmentScore, double? MidtermScore, double? PriorGpa, bool Failed);

public record MlTrainRequest(List<MlTrainRow> Rows, bool IncludeBase, string Activate);

public record MlTrainResponse(MlModelInfo Model, bool Activated, string? PreviousVersion);

/// <summary>Lỗi khi gọi ML service. Status = 503 nghĩa là không liên lạc được; mã 4xx là ML service từ chối yêu cầu.</summary>
public class MlException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public interface IMlClient
{
    Task<bool> IsHealthyAsync();
    Task<MlPredictResponse> PredictAsync(IReadOnlyList<MlPredictItem> items);
    Task<List<MlModelInfo>> ListModelsAsync();
    Task<MlTrainResponse> TrainAsync(IReadOnlyList<MlTrainRow> rows, bool includeBase, string activate = "auto");
    Task<MlModelInfo> ActivateAsync(string version);
}

public class MlClient(HttpClient http, ILogger<MlClient> logger) : IMlClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            using var response = await http.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    public Task<MlPredictResponse> PredictAsync(IReadOnlyList<MlPredictItem> items) =>
        SendAsync<MlPredictResponse>(HttpMethod.Post, "/predict", new { items });

    public Task<List<MlModelInfo>> ListModelsAsync() => SendAsync<List<MlModelInfo>>(HttpMethod.Get, "/models");

    public Task<MlTrainResponse> TrainAsync(IReadOnlyList<MlTrainRow> rows, bool includeBase, string activate = "auto") =>
        SendAsync<MlTrainResponse>(HttpMethod.Post, "/train", new MlTrainRequest([.. rows], includeBase, activate));

    public Task<MlModelInfo> ActivateAsync(string version) =>
        SendAsync<MlModelInfo>(HttpMethod.Post, "/activate", new { version });

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: Json);
            }
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await ReadDetailAsync(response);
                logger.LogWarning("ML service trả {Status} cho {Method} {Path}: {Detail}", (int)response.StatusCode, method, path, detail);
                // 4xx: ML từ chối yêu cầu (ví dụ không đủ dữ liệu); còn lại coi như ML không khả dụng
                var status = (int)response.StatusCode is >= 400 and < 500 ? (int)response.StatusCode : (int)HttpStatusCode.ServiceUnavailable;
                throw new MlException(status, status == 503 ? "ML service không khả dụng" : detail);
            }
            return (await response.Content.ReadFromJsonAsync<T>(Json))!;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "Không gọi được ML service ({Method} {Path})", method, path);
            throw new MlException((int)HttpStatusCode.ServiceUnavailable, "ML service không khả dụng");
        }
    }

    private static async Task<string> ReadDetailAsync(HttpResponseMessage response)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String
                ? detail.GetString()! : $"ML service trả lỗi {(int)response.StatusCode}";
        }
        catch (JsonException)
        {
            return $"ML service trả lỗi {(int)response.StatusCode}";
        }
    }
}
