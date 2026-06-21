using System.Collections.Generic;

namespace TradingService.Application.Common.Models
{
    /// <summary>
    /// Simple paging container.
    /// </summary>
    public sealed class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; }
        public int Page { get; }
        public int PageSize { get; }
        public long TotalCount { get; }
        public int TotalPages => (int)System.Math.Ceiling((double)TotalCount / PageSize);

        public PagedResult(IReadOnlyList<T> items, int page, int pageSize, long totalCount)
        {
            Items = items;
            Page = page;
            PageSize = pageSize;
            TotalCount = totalCount;
        }
    }
}
