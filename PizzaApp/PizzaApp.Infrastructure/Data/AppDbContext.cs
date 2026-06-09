using Microsoft.EntityFrameworkCore;
using PizzaApp.Core.Entities;

namespace PizzaApp.Infrastructure.Data;

// LƯU Ý: Nếu bạn dùng hoàn toàn MongoDB, bạn có thể xóa file này và AppDbContext
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Seed User mẫu - Chuyển ID sang String
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = "1",
                FullName = "Guest User",
                Email = "guest@example.com",
                PasswordHash = "fake_hash",
                Address = "123 Test St",
                PhoneNumber = "0123456789"
            }
        );

        // Seed Products - Chuyển ID sang String
        modelBuilder.Entity<Product>().HasData(
            new Product { Id = "1", Name = "Margherita", Description = "Phô mai, cà chua, húng quế", Price = 89000, Category = "Truyền thống", ImageUrl = "margherita.jpg", IsAvailable = true },
            new Product { Id = "2", Name = "Hải Sản", Description = "Tôm, mực, sốt tỏi bơ", Price = 129000, Category = "Hải sản", ImageUrl = "seafood.jpg", IsAvailable = true },
            new Product { Id = "3", Name = "BBQ Bò", Description = "Thịt bò, hành tây, sốt BBQ", Price = 119000, Category = "Đặc biệt", ImageUrl = "bbq.jpg", IsAvailable = true },
            new Product { Id = "4", Name = "Veggie", Description = "Rau củ, nấm, phô mai", Price = 79000, Category = "Chay", ImageUrl = "veggie.jpg", IsAvailable = true }
        );
    }
}
