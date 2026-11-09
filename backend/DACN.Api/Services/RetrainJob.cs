using System.Threading.Channels;
using DACN.Api.Dtos;

namespace DACN.Api.Services;

/// <summary>
/// Huấn luyện lại mô hình chạy nền (một lần tại một thời điểm). Endpoint trả 202 ngay; kết quả xem ở GET /api/ml/retrain/status
/// và phiên bản mới xuất hiện trong GET /api/ml/models.
/// </summary>
public class RetrainJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<RetrainJob> logger) : BackgroundService
{
    private readonly Channel<bool> _queue = Channel.CreateBounded<bool>(1);
    private readonly object _gate = new();
    private RetrainStatusDto _status = new("Idle", null, null, null, null, null);

    public RetrainStatusDto Status
    {
        get { lock (_gate) return _status; }
    }

    /// <summary>Bắt đầu huấn luyện. Trả false nếu đang có một lần huấn luyện khác.</summary>
    public bool TryStart()
    {
        lock (_gate)
        {
            if (_status.Status == "Running")
            {
                return false;
            }
            _status = new RetrainStatusDto("Running", "Đang huấn luyện...", DateTime.UtcNow, null, null, null);
        }
        _queue.Writer.TryWrite(true);
        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var _ in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await RunAsync();
        }
    }

    private async Task RunAsync()
    {
        var started = Status.StartedAt;
        try
        {
            using var scope = scopes.CreateScope();
            var features = scope.ServiceProvider.GetRequiredService<MlFeatureService>();
            var ml = scope.ServiceProvider.GetRequiredService<IMlClient>();
            var models = scope.ServiceProvider.GetRequiredService<MlModelService>();

            var rows = await features.TrainingRowsAsync();
            // Ít dữ liệu thật thì gộp thêm bộ dữ liệu nền đóng gói trong ML service
            var includeBase = rows.Count < config.GetValue("Ml:MinRowsWithoutBase", 400);
            var result = await ml.TrainAsync(
                [.. rows.Select(r => new MlTrainRow(r.AbsentRate, r.LateRate, r.AssignmentScore, r.MidtermScore, r.PriorGpa, r.Failed))],
                includeBase);
            await models.SyncAsync();

            var m = result.Model;
            var note = result.Activated ? "Đã đưa vào sử dụng." : "Chưa dùng vì kém mô hình hiện tại, có thể kích hoạt thủ công.";
            Finish("Succeeded", $"Đã huấn luyện {m.Version} ({m.Algorithm}) trên {m.NRows} dòng, ROC-AUC {m.Metrics.RocAuc:0.000}. {note}",
                started, m.Version, result.Activated);
        }
        catch (MlException ex)
        {
            Finish("Failed", ex.Message, started, null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Huấn luyện lại thất bại");
            Finish("Failed", "Huấn luyện thất bại do lỗi không mong đợi, xem log của backend", started, null, null);
        }
    }

    private void Finish(string status, string message, DateTime? started, string? version, bool? activated)
    {
        lock (_gate)
        {
            _status = new RetrainStatusDto(status, message, started, DateTime.UtcNow, version, activated);
        }
    }
}
