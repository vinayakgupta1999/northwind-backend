namespace Northwind.Recommendations.API.Models;

/// <summary>
/// Generic paged response wrapper - har API endpoint yahi return karta hai.
/// Frontend ko totalCount mil jaata hai aur uske basis par pagination karta hai.
/// </summary>
public class PagedResponse<T>
{
    /// <summary>Current page number (1-based)</summary>
    public int Page { get; set; }

    /// <summary>Items per page</summary>
    public int PageSize { get; set; }

    /// <summary>Total records available in DB (without pagination)</summary>
    public int TotalCount { get; set; }

    /// <summary>Total pages = ceil(TotalCount / PageSize)</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>Is there a next page?</summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>Is there a previous page?</summary>
    public bool HasPrev => Page > 1;

    /// <summary>Actual data for this page</summary>
    public List<T> Data { get; set; } = new();
}
