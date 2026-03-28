using Microsoft.EntityFrameworkCore;
using Northwind.Recommendations.API.Models;

namespace Northwind.Recommendations.API.Data;

public class NorthwindDbContext : DbContext
{
    public NorthwindDbContext(DbContextOptions<NorthwindDbContext> options) : base(options) { }

    public DbSet<Customer>    Customers    { get; set; }
    public DbSet<Product>     Products     { get; set; }
    public DbSet<Category>    Categories   { get; set; }
    public DbSet<Supplier>    Suppliers    { get; set; }
    public DbSet<Order>       Orders       { get; set; }
    public DbSet<OrderDetail> OrderDetails { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Customer>().ToTable("customers");
        mb.Entity<Product>().ToTable("products");
        mb.Entity<Category>().ToTable("categories");
        mb.Entity<Supplier>().ToTable("suppliers");
        mb.Entity<Order>().ToTable("orders");
        mb.Entity<OrderDetail>().ToTable("order_details");

        mb.Entity<Customer>(e => {
            e.HasKey(c => c.CustomerID);
            e.Property(c => c.CustomerID).HasColumnName("customer_id").HasColumnType("bpchar");
            e.Property(c => c.CompanyName).HasColumnName("company_name");
            e.Property(c => c.ContactName).HasColumnName("contact_name");
            e.Property(c => c.ContactTitle).HasColumnName("contact_title");
            e.Property(c => c.Country).HasColumnName("country");
            e.Property(c => c.City).HasColumnName("city");
        });

        mb.Entity<Category>(e => {
            e.HasKey(c => c.CategoryID);
            e.Property(c => c.CategoryID).HasColumnName("category_id");
            e.Property(c => c.CategoryName).HasColumnName("category_name");
            e.Property(c => c.Description).HasColumnName("description");
        });

        mb.Entity<Supplier>(e => {
            e.HasKey(s => s.SupplierID);
            e.Property(s => s.SupplierID).HasColumnName("supplier_id");
            e.Property(s => s.CompanyName).HasColumnName("company_name");
            e.Property(s => s.Country).HasColumnName("country");
        });

        mb.Entity<Product>(e => {
            e.HasKey(p => p.ProductID);
            e.Property(p => p.ProductID).HasColumnName("product_id");
            e.Property(p => p.ProductName).HasColumnName("product_name");
            e.Property(p => p.SupplierID).HasColumnName("supplier_id");
            e.Property(p => p.CategoryID).HasColumnName("category_id");
            e.Property(p => p.UnitPrice).HasColumnName("unit_price");
            e.Property(p => p.UnitsInStock).HasColumnName("units_in_stock");
            e.Property(p => p.Discontinued).HasColumnName("discontinued");
        });

        mb.Entity<Order>(e => {
            e.HasKey(o => o.OrderID);
            e.Property(o => o.OrderID).HasColumnName("order_id");
            e.Property(o => o.CustomerID).HasColumnName("customer_id").HasColumnType("bpchar");
            e.Property(o => o.OrderDate).HasColumnName("order_date");
        });

        mb.Entity<OrderDetail>(e => {
            e.HasKey(od => new { od.OrderID, od.ProductID });
            e.Property(od => od.OrderID).HasColumnName("order_id");
            e.Property(od => od.ProductID).HasColumnName("product_id");
            e.Property(od => od.UnitPrice).HasColumnName("unit_price");
            e.Property(od => od.Quantity).HasColumnName("quantity");
            e.Property(od => od.Discount).HasColumnName("discount");
        });

        mb.Entity<OrderDetail>()
            .HasOne(od => od.Order).WithMany(o => o.OrderDetails).HasForeignKey(od => od.OrderID);
        mb.Entity<OrderDetail>()
            .HasOne(od => od.Product).WithMany(p => p.OrderDetails).HasForeignKey(od => od.ProductID);
        mb.Entity<Order>()
            .HasOne(o => o.Customer).WithMany(c => c.Orders).HasForeignKey(o => o.CustomerID);
        mb.Entity<Product>()
            .HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryID);
        mb.Entity<Product>()
            .HasOne(p => p.Supplier).WithMany(s => s.Products).HasForeignKey(p => p.SupplierID);
    }
}
