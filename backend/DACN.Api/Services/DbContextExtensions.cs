using DACN.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Services;

public static class DbContextExtensions
{
    /// <summary>
    /// Chạy nhiều thao tác trong một transaction. Bắt buộc bọc trong execution strategy vì
    /// AddDbContext đã bật EnableRetryOnFailure (không cho phép BeginTransaction trực tiếp).
    /// </summary>
    public static Task<T> InTransactionAsync<T>(this AppDbContext db, Func<Task<T>> action) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var result = await action();
            await transaction.CommitAsync();
            return result;
        });
}
