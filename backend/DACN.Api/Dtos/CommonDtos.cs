namespace DACN.Api.Dtos;

/// <summary>Kết quả phân trang. Page bắt đầu từ 1.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems);
