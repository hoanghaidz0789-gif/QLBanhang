using final.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace final.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>().Property(product => product.Price).HasPrecision(18, 2);
        builder.Entity<Order>().Property(order => order.TotalAmount).HasPrecision(18, 2);
        builder.Entity<Order>().HasIndex(order => order.ConfirmationToken).IsUnique();
        builder.Entity<OrderItem>().Property(item => item.UnitPrice).HasPrecision(18, 2);
        builder.Entity<OrderItem>().HasOne(item => item.Product).WithMany()
            .HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<OrderItem>().HasOne(item => item.Order).WithMany(order => order.Items)
            .HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
