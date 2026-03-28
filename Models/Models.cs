using System.ComponentModel.DataAnnotations;

namespace Northwind.Recommendations.API.Models;

// DDL types: smallint→short, real→float, integer→int, bpchar→string

public class Customer
{
    [Key] public string CustomerID { get; set; } = string.Empty;
    public string  CompanyName  { get; set; } = string.Empty;
    public string? ContactName  { get; set; }
    public string? ContactTitle { get; set; }
    public string? Country      { get; set; }
    public string? City         { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class Category
{
    [Key] public short  CategoryID   { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description  { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Supplier
{
    [Key] public short  SupplierID  { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Country    { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product
{
    [Key] public short  ProductID    { get; set; }
    public string ProductName  { get; set; } = string.Empty;
    public short?  SupplierID   { get; set; }
    public short?  CategoryID   { get; set; }
    public float?  UnitPrice    { get; set; }
    public short?  UnitsInStock { get; set; }
    public int     Discontinued { get; set; }   // integer: 0=active, 1=discontinued
    public Category? Category { get; set; }
    public Supplier? Supplier { get; set; }
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}

public class Order
{
    [Key] public short   OrderID    { get; set; }
    public string? CustomerID { get; set; }
    public DateTime? OrderDate { get; set; }
    public Customer? Customer  { get; set; }
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}

public class OrderDetail
{
    public short OrderID   { get; set; }
    public short ProductID { get; set; }
    public float UnitPrice { get; set; }
    public short Quantity  { get; set; }
    public float Discount  { get; set; }
    public Order?   Order   { get; set; }
    public Product? Product { get; set; }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public class RecommendedProductDto
{
    public int    ProductID          { get; set; }
    public string ProductName        { get; set; } = string.Empty;
    public double UnitPrice          { get; set; }
    public int    UnitsInStock        { get; set; }
    public string CategoryName       { get; set; } = string.Empty;
    public string SupplierName       { get; set; } = string.Empty;
    public double Score              { get; set; }
    public string RecommendationType { get; set; } = string.Empty;
    public string? ReasonLabel       { get; set; }
}

/// <summary>
/// Hybrid response wraps paged data + customer metadata
/// </summary>
public class HybridRecommendationResponseDto
{
    public string CustomerId   { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Segment      { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    // Paged recommendations
    public int    Page       { get; set; }
    public int    PageSize   { get; set; }
    public int    TotalCount { get; set; }
    public int    TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool   HasNext    => Page < TotalPages;
    public bool   HasPrev    => Page > 1;
    public List<RecommendedProductDto> Data { get; set; } = new();
}

public class CustomerSummaryDto
{
    public string    CustomerID    { get; set; } = string.Empty;
    public string    CompanyName   { get; set; } = string.Empty;
    public string?   Country       { get; set; }
    public int       TotalOrders   { get; set; }
    public double    TotalSpend    { get; set; }
    public string    Segment       { get; set; } = string.Empty;
    public DateTime? LastOrderDate { get; set; }
}

public class TrendingProductDto
{
    public int    ProductID          { get; set; }
    public string ProductName        { get; set; } = string.Empty;
    public double UnitPrice          { get; set; }
    public string CategoryName       { get; set; } = string.Empty;
    public string SupplierName       { get; set; } = string.Empty;
    public int    UniqueCustomers    { get; set; }
    public int    TotalOrders        { get; set; }
    public int    TotalQuantitySold  { get; set; }
    public double TotalRevenue       { get; set; }
    public double DailySalesVelocity { get; set; }
}

public class FrequentlyBoughtTogetherDto
{
    public int    ProductID         { get; set; }
    public string ProductName       { get; set; } = string.Empty;
    public double UnitPrice         { get; set; }
    public string CategoryName      { get; set; } = string.Empty;
    public int    CoOccurrenceCount { get; set; }
    public double Support           { get; set; }
    public double Lift              { get; set; }
}
