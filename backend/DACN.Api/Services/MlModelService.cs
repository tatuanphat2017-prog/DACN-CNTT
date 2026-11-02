using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Services;

/// <summary>Đồng bộ danh sách phiên bản mô hình giữa ML service (nơi giữ file mô hình) và bảng ModelVersions trong database.</summary>
public class MlModelService(AppDbContext db, IMlClient ml)
{
    /// <summary>Lấy danh sách từ ML service, thêm phiên bản còn thiếu, cập nhật phiên bản đang dùng (chỉ một bản IsActive).</summary>
    public async Task SyncAsync()
    {
        var remote = await ml.ListModelsAsync();
        var known = await db.ModelVersions.ToDictionaryAsync(m => m.Version);

        foreach (var info in remote.Where(r => !known.ContainsKey(r.Version)))
        {
            var row = new ModelVersion
            {
                Version = info.Version, Algorithm = info.Algorithm,
                Accuracy = info.Metrics.Accuracy, Precision = info.Metrics.Precision, Recall = info.Metrics.Recall,
                F1 = info.Metrics.F1, RocAuc = info.Metrics.RocAuc,
                FilePath = $"{info.Version}.joblib", TrainedAt = info.TrainedAt.UtcDateTime, IsActive = false,
            };
            db.ModelVersions.Add(row);
            known[info.Version] = row;
        }
        await db.SaveChangesAsync();

        var active = remote.FirstOrDefault(r => r.IsActive)?.Version;
        await SetActiveAsync(active);
    }

    /// <summary>Đặt đúng một phiên bản là đang dùng. Bỏ cờ của bản cũ trước vì có unique index cho IsActive = 1.</summary>
    public Task SetActiveAsync(string? version) =>
        db.InTransactionAsync(async () =>
        {
            await db.ModelVersions.Where(m => m.IsActive && m.Version != version).ExecuteUpdateAsync(m => m.SetProperty(x => x.IsActive, false));
            if (version is not null)
            {
                await db.ModelVersions.Where(m => m.Version == version).ExecuteUpdateAsync(m => m.SetProperty(x => x.IsActive, true));
            }
            return true;
        });

    /// <summary>Tìm dòng ModelVersions của một phiên bản; chưa có thì đồng bộ từ ML service.</summary>
    public async Task<ModelVersion> EnsureVersionAsync(string version)
    {
        var row = await db.ModelVersions.FirstOrDefaultAsync(m => m.Version == version);
        if (row is not null)
        {
            return row;
        }

        await SyncAsync();
        return await db.ModelVersions.FirstOrDefaultAsync(m => m.Version == version)
               ?? throw new MlException(503, $"Không tìm thấy thông tin phiên bản mô hình {version}");
    }

    public async Task<List<ModelVersionItem>> ListAsync() =>
        await db.ModelVersions.AsNoTracking().OrderByDescending(m => m.TrainedAt)
            .Select(m => new ModelVersionItem(m.Id, m.Version, m.Algorithm, m.Accuracy, m.Precision, m.Recall, m.F1, m.RocAuc, m.TrainedAt, m.IsActive))
            .ToListAsync();
}
