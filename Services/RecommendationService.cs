using Microsoft.EntityFrameworkCore;
using Northwind.Recommendations.API.Data;
using Northwind.Recommendations.API.Models;

namespace Northwind.Recommendations.API.Services;

public interface IRecommendationService
{
    // Paged versions — page & pageSize se backend slice karta hai
    Task<HybridRecommendationResponseDto>        GetHybridRecommendationsAsync(string customerId, int page, int pageSize);
    Task<PagedResponse<RecommendedProductDto>>   GetCollaborativeRecommendationsAsync(string customerId, int page, int pageSize);
    Task<PagedResponse<RecommendedProductDto>>   GetContentBasedRecommendationsAsync(string customerId, int page, int pageSize);
    Task<PagedResponse<TrendingProductDto>>      GetTrendingProductsAsync(int days, int page, int pageSize);
    Task<PagedResponse<FrequentlyBoughtTogetherDto>> GetFrequentlyBoughtTogetherAsync(int productId, int page, int pageSize);
    Task<List<CustomerSummaryDto>>               GetCustomersAsync();
    Task<CustomerSummaryDto?>                    GetCustomerSummaryAsync(string customerId);
}

public class RecommendationService : IRecommendationService
{
    private readonly NorthwindDbContext _db;
    public RecommendationService(NorthwindDbContext db) => _db = db;

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: build PagedResponse from a full in-memory list
    // ─────────────────────────────────────────────────────────────────────────
    private static PagedResponse<T> Paginate<T>(List<T> all, int page, int pageSize)
    {
        int total  = all.Count;
        int safePs = Math.Max(1, pageSize);
        int safePg = Math.Max(1, page);

        var data = all
            .Skip((safePg - 1) * safePs)
            .Take(safePs)
            .ToList();

        return new PagedResponse<T>
        {
            Page      = safePg,
            PageSize  = safePs,
            TotalCount = total,
            Data      = data
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. TRENDING
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<PagedResponse<TrendingProductDto>> GetTrendingProductsAsync(int days, int page, int pageSize)
    {
        var since = DateTime.UtcNow.AddDays(-days);

        // Load flat rows then group in memory (avoids EF GroupBy translation)
        var rows = await _db.OrderDetails
            .Where(od => od.Order!.OrderDate >= since && od.Product!.Discontinued == 0)
            .Select(od => new
            {
                ProductID    = (int)od.ProductID,
                ProductName  = od.Product!.ProductName,
                UnitPrice    = (double)(od.Product!.UnitPrice ?? 0f),
                CategoryName = od.Product!.Category!.CategoryName,
                SupplierName = od.Product!.Supplier!.CompanyName,
                CustomerID   = od.Order!.CustomerID,
                OrderID      = (int)od.OrderID,
                Qty          = (int)od.Quantity,
                LineTotal    = (double)od.Quantity * (double)od.UnitPrice * (1.0 - (double)od.Discount)
            })
            .ToListAsync();

        var all = rows
            .GroupBy(r => r.ProductID)
            .Select(g => new TrendingProductDto
            {
                ProductID          = g.Key,
                ProductName        = g.First().ProductName,
                UnitPrice          = Math.Round(g.First().UnitPrice, 2),
                CategoryName       = g.First().CategoryName,
                SupplierName       = g.First().SupplierName,
                UniqueCustomers    = g.Select(r => r.CustomerID).Distinct().Count(),
                TotalOrders        = g.Select(r => r.OrderID).Distinct().Count(),
                TotalQuantitySold  = g.Sum(r => r.Qty),
                TotalRevenue       = Math.Round(g.Sum(r => r.LineTotal), 2),
                DailySalesVelocity = Math.Round((double)g.Sum(r => r.Qty) / Math.Max(1, days), 2)
            })
            .OrderByDescending(t => t.TotalRevenue)
            .ToList();

        return Paginate(all, page, pageSize);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. COLLABORATIVE
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<PagedResponse<RecommendedProductDto>> GetCollaborativeRecommendationsAsync(
        string customerId, int page, int pageSize)
    {
        var custId = customerId.Trim();

        var myRows = await _db.Orders
            .Where(o => o.CustomerID == custId)
            .SelectMany(o => o.OrderDetails.Select(od => new { ProductID = (int)od.ProductID }))
            .ToListAsync();

        if (!myRows.Any()) return Paginate(new List<RecommendedProductDto>(), page, pageSize);

        var myPids      = myRows.Select(r => r.ProductID).Distinct().ToList();
        var myPidsShort = myPids.Select(id => (short)id).ToList();

        var similarCustIds = await _db.Orders
            .Where(o => o.CustomerID != custId &&
                        o.OrderDetails.Any(od => myPidsShort.Contains(od.ProductID)))
            .Select(o => o.CustomerID)
            .Distinct()
            .ToListAsync();

        if (!similarCustIds.Any()) return Paginate(new List<RecommendedProductDto>(), page, pageSize);

        var theirRows = await _db.Orders
            .Where(o => similarCustIds.Contains(o.CustomerID))
            .SelectMany(o => o.OrderDetails.Select(od => new
            {
                CustomerID   = o.CustomerID,
                ProductID    = (int)od.ProductID,
                Qty          = (int)od.Quantity,
                Discontinued = od.Product!.Discontinued
            }))
            .Where(r => r.Discontinued == 0)
            .ToListAsync();

        var all = theirRows
            .Where(r => !myPids.Contains(r.ProductID))
            .GroupBy(r => r.ProductID)
            .Select(g => new
            {
                ProductID       = g.Key,
                SharedCustomers = g.Select(r => r.CustomerID).Distinct().Count(),
                TotalQty        = g.Sum(r => r.Qty)
            })
            .OrderByDescending(r => r.SharedCustomers)
            .ThenByDescending(r => r.TotalQty)
            .ToList();

        // Fetch product details for all candidates
        var allPidsShort = all.Select(a => (short)a.ProductID).ToList();
        var products = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Where(p => allPidsShort.Contains(p.ProductID))
            .ToDictionaryAsync(p => (int)p.ProductID);

        var dtos = all
            .Where(a => products.ContainsKey(a.ProductID))
            .Select(a =>
            {
                var p = products[a.ProductID];
                return new RecommendedProductDto
                {
                    ProductID          = a.ProductID,
                    ProductName        = p.ProductName,
                    UnitPrice          = Math.Round((double)(p.UnitPrice ?? 0f), 2),
                    UnitsInStock       = p.UnitsInStock ?? 0,
                    CategoryName       = p.Category?.CategoryName ?? "Unknown",
                    SupplierName       = p.Supplier?.CompanyName  ?? "Unknown",
                    Score              = a.SharedCustomers,
                    RecommendationType = "Collaborative",
                    ReasonLabel        = $"{a.SharedCustomers} similar customers bought this"
                };
            })
            .ToList();

        return Paginate(dtos, page, pageSize);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. CONTENT-BASED
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<PagedResponse<RecommendedProductDto>> GetContentBasedRecommendationsAsync(
        string customerId, int page, int pageSize)
    {
        var custId = customerId.Trim();

        var myRows = await _db.Orders
            .Where(o => o.CustomerID == custId)
            .SelectMany(o => o.OrderDetails.Select(od => new
            {
                ProductID  = (int)od.ProductID,
                CategoryID = od.Product!.CategoryID.HasValue ? (int)od.Product.CategoryID.Value : 0,
                SupplierID = od.Product!.SupplierID.HasValue ? (int)od.Product.SupplierID.Value : 0,
                UnitPrice  = (double)(od.Product!.UnitPrice ?? 0f)
            }))
            .ToListAsync();

        if (!myRows.Any()) return Paginate(new List<RecommendedProductDto>(), page, pageSize);

        var myProductIdsShort = myRows.Select(r => r.ProductID).Distinct().Select(id => (short)id).ToList();
        var myCatIds          = myRows.Select(r => (short)r.CategoryID).Distinct().ToList();
        var mySupIds          = myRows.Select(r => (short)r.SupplierID).Distinct().ToList();
        var avgPrice          = myRows.Average(r => r.UnitPrice);

        var candidates = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Where(p => p.Discontinued == 0
                     && p.UnitsInStock > 0
                     && !myProductIdsShort.Contains(p.ProductID)
                     && (myCatIds.Contains(p.CategoryID!.Value) ||
                         mySupIds.Contains(p.SupplierID!.Value)))
            .ToListAsync();

        var dtos = candidates
            .Select(p =>
            {
                double score = 0;
                if (p.CategoryID.HasValue && myCatIds.Contains(p.CategoryID.Value)) score += 2.0;
                if (p.SupplierID.HasValue && mySupIds.Contains(p.SupplierID.Value))  score += 1.0;
                var diff = Math.Abs((double)(p.UnitPrice ?? 0f) - avgPrice) / (avgPrice + 1);
                if (diff < 0.3) score += 0.5;
                return new RecommendedProductDto
                {
                    ProductID          = p.ProductID,
                    ProductName        = p.ProductName,
                    UnitPrice          = Math.Round((double)(p.UnitPrice ?? 0f), 2),
                    UnitsInStock       = p.UnitsInStock ?? 0,
                    CategoryName       = p.Category?.CategoryName ?? "Unknown",
                    SupplierName       = p.Supplier?.CompanyName  ?? "Unknown",
                    Score              = score,
                    RecommendationType = "ContentBased",
                    ReasonLabel        = "Matches your favourite categories"
                };
            })
            .OrderByDescending(r => r.Score)
            .ToList();

        return Paginate(dtos, page, pageSize);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. HYBRID — combines all three, returns paged + customer meta
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<HybridRecommendationResponseDto> GetHybridRecommendationsAsync(
        string customerId, int page, int pageSize)
    {
        var custId   = customerId.Trim();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerID == custId);
        if (customer == null) throw new KeyNotFoundException($"Customer '{customerId}' not found.");

        // Get ALL results from each source (no limit — we'll paginate after blend)
        var collabAll   = (await GetCollaborativeRecommendationsAsync(custId, 1, int.MaxValue)).Data;
        var contentAll  = (await GetContentBasedRecommendationsAsync(custId, 1, int.MaxValue)).Data;
        var trendingAll = (await GetTrendingProductsAsync(90, 1, int.MaxValue)).Data;

        var map = new Dictionary<int, (double c, double ct, double t)>();

        double maxC  = collabAll.Any()   ? collabAll.Max(r => r.Score)     : 1;
        double maxCt = contentAll.Any()  ? contentAll.Max(r => r.Score)    : 1;
        double maxT  = trendingAll.Any() ? trendingAll.Max(t => t.TotalRevenue) : 1;

        foreach (var r in collabAll)
            map[r.ProductID] = (r.Score / maxC, 0, 0);

        foreach (var r in contentAll)
        { var e = map.GetValueOrDefault(r.ProductID); map[r.ProductID] = (e.c, r.Score / maxCt, e.t); }

        foreach (var t in trendingAll)
        { var e = map.GetValueOrDefault(t.ProductID); map[t.ProductID] = (e.c, e.ct, t.TotalRevenue / maxT); }

        var pidsShort = map.Keys.Select(id => (short)id).ToList();
        var products  = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Where(p => pidsShort.Contains(p.ProductID) && p.Discontinued == 0)
            .ToListAsync();

        // Full sorted list
        var allDtos = products.Select(p =>
        {
            var s     = map.GetValueOrDefault(p.ProductID);
            var score = (s.c * 0.5) + (s.ct * 0.3) + (s.t * 0.2);
            var reason = s.c > s.ct && s.c > s.t   ? "Similar customers also bought this"
                       : s.ct > s.t                 ? "Matches your favourite categories"
                                                    : "Trending in last 90 days";
            return new RecommendedProductDto
            {
                ProductID          = p.ProductID,
                ProductName        = p.ProductName,
                UnitPrice          = Math.Round((double)(p.UnitPrice ?? 0f), 2),
                UnitsInStock       = p.UnitsInStock ?? 0,
                CategoryName       = p.Category?.CategoryName ?? "Unknown",
                SupplierName       = p.Supplier?.CompanyName  ?? "Unknown",
                Score              = Math.Round(score, 4),
                RecommendationType = "Hybrid",
                ReasonLabel        = reason
            };
        })
        .OrderByDescending(r => r.Score)
        .ToList();

        int total  = allDtos.Count;
        int safePs = Math.Max(1, pageSize);
        int safePg = Math.Max(1, page);
        var pageData = allDtos.Skip((safePg - 1) * safePs).Take(safePs).ToList();

        var custSummary = await GetCustomerSummaryAsync(custId);

        return new HybridRecommendationResponseDto
        {
            CustomerId   = custId,
            CustomerName = customer.CompanyName,
            Segment      = custSummary?.Segment ?? "Active",
            GeneratedAt  = DateTime.UtcNow,
            Page         = safePg,
            PageSize     = safePs,
            TotalCount   = total,
            Data         = pageData
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. FREQUENTLY BOUGHT TOGETHER
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<PagedResponse<FrequentlyBoughtTogetherDto>> GetFrequentlyBoughtTogetherAsync(
        int productId, int page, int pageSize)
    {
        var shortId = (short)productId;

        var orderIdsWithProduct = await _db.OrderDetails
            .Where(od => od.ProductID == shortId)
            .Select(od => od.OrderID)
            .Distinct()
            .ToListAsync();

        int totalWith = orderIdsWithProduct.Count;
        if (totalWith == 0) return Paginate(new List<FrequentlyBoughtTogetherDto>(), page, pageSize);

        int totalOrders = await _db.Orders.CountAsync();

        var coRows = await _db.OrderDetails
            .Where(od => orderIdsWithProduct.Contains(od.OrderID)
                      && od.ProductID != shortId
                      && od.Product!.Discontinued == 0)
            .Select(od => new
            {
                ProductID    = (int)od.ProductID,
                ProductName  = od.Product!.ProductName,
                UnitPrice    = (double)(od.Product!.UnitPrice ?? 0f),
                CategoryName = od.Product!.Category!.CategoryName,
                OrderID      = od.OrderID
            })
            .ToListAsync();

        var grouped = coRows
            .GroupBy(r => r.ProductID)
            .Select(g =>
            {
                int coCount = g.Select(r => r.OrderID).Distinct().Count();
                return new FrequentlyBoughtTogetherDto
                {
                    ProductID         = g.Key,
                    ProductName       = g.First().ProductName,
                    UnitPrice         = Math.Round(g.First().UnitPrice, 2),
                    CategoryName      = g.First().CategoryName,
                    CoOccurrenceCount = coCount,
                    Support           = Math.Round((double)coCount / totalWith, 4),
                    Lift              = 0
                };
            })
            .OrderByDescending(r => r.CoOccurrenceCount)
            .ToList();

        // Batch lift calculation
        var gPidsShort = grouped.Select(g => (short)g.ProductID).ToList();
        var freqRows   = await _db.OrderDetails
            .Where(od => gPidsShort.Contains(od.ProductID))
            .Select(od => new { ProductID = (int)od.ProductID, OrderID = od.OrderID })
            .ToListAsync();

        var freqDict = freqRows
            .GroupBy(r => r.ProductID)
            .ToDictionary(g => g.Key, g => g.Select(r => r.OrderID).Distinct().Count());

        foreach (var item in grouped)
            if (freqDict.TryGetValue(item.ProductID, out int cnt) && cnt > 0)
                item.Lift = Math.Round(item.Support / ((double)cnt / totalOrders), 4);

        return Paginate(grouped.OrderByDescending(r => r.Lift).ToList(), page, pageSize);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. CUSTOMERS
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<List<CustomerSummaryDto>> GetCustomersAsync()
    {
        var customers = await _db.Customers.OrderBy(c => c.CompanyName).ToListAsync();

        var statsRows = await _db.Orders
            .SelectMany(o => o.OrderDetails.Select(od => new
            {
                CustomerID = o.CustomerID,
                OrderID    = (int)o.OrderID,
                OrderDate  = o.OrderDate,
                LineTotal  = (double)od.Quantity * (double)od.UnitPrice * (1.0 - (double)od.Discount)
            }))
            .ToListAsync();

        var stats = statsRows
            .GroupBy(r => r.CustomerID!.Trim())
            .ToDictionary(
                g => g.Key,
                g => (
                    Orders: g.Select(r => r.OrderID).Distinct().Count(),
                    Spend:  g.Sum(r => r.LineTotal),
                    Last:   g.Max(r => r.OrderDate)
                ));

        return customers.Select(c =>
        {
            var cid = c.CustomerID.Trim();
            stats.TryGetValue(cid, out var s);
            return new CustomerSummaryDto
            {
                CustomerID    = cid,
                CompanyName   = c.CompanyName,
                Country       = c.Country,
                TotalOrders   = s.Orders,
                TotalSpend    = Math.Round(s.Spend, 2),
                Segment       = ComputeSegment(s.Spend, s.Orders, s.Last),
                LastOrderDate = s.Last
            };
        }).ToList();
    }

    public async Task<CustomerSummaryDto?> GetCustomerSummaryAsync(string customerId)
    {
        var custId   = customerId.Trim();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerID == custId);
        if (customer == null) return null;

        var rows = await _db.Orders
            .Where(o => o.CustomerID == custId)
            .SelectMany(o => o.OrderDetails.Select(od => new
            {
                OrderID   = (int)o.OrderID,
                OrderDate = o.OrderDate,
                LineTotal = (double)od.Quantity * (double)od.UnitPrice * (1.0 - (double)od.Discount)
            }))
            .ToListAsync();

        double spend  = rows.Sum(r => r.LineTotal);
        int    orders = rows.Select(r => r.OrderID).Distinct().Count();
        var    last   = rows.Any() ? rows.Max(r => r.OrderDate) : (DateTime?)null;

        return new CustomerSummaryDto
        {
            CustomerID    = custId,
            CompanyName   = customer.CompanyName,
            Country       = customer.Country,
            TotalOrders   = orders,
            TotalSpend    = Math.Round(spend, 2),
            Segment       = ComputeSegment(spend, orders, last),
            LastOrderDate = last
        };
    }

    private static string ComputeSegment(double spend, int orders, DateTime? last)
    {
        if (spend > 10000 && orders > 10) return "VIP";
        if (spend > 5000  && orders > 5)  return "Loyal";
        if (last.HasValue && (DateTime.UtcNow - last.Value).TotalDays < 90)  return "Active";
        if (last.HasValue && (DateTime.UtcNow - last.Value).TotalDays < 180) return "At Risk";
        return "Dormant";
    }
}
