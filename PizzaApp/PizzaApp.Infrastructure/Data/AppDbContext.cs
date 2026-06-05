using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using PizzaApp.Core.Entities;

namespace PizzaApp.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Margherita", Description = "Phô mai, cà chua, húng quế", Price = 89000, Category = "Truyền thống", ImageUrl = "margherita.jpg", IsAvailable = true },
            new Product { Id = 2, Name = "Hải Sản", Description = "Tôm, mực, sốt tỏi bơ", Price = 129000, Category = "Hải sản", ImageUrl = "seafood.jpg", IsAvailable = true },
            new Product { Id = 3, Name = "BBQ Bò", Description = "Thịt bò, hành tây, sốt BBQ", Price = 119000, Category = "Đặc biệt", ImageUrl = "bbq.jpg", IsAvailable = true },
            new Product { Id = 4, Name = "Veggie", Description = "Rau củ, nấm, phô mai", Price = 79000, Category = "Chay", ImageUrl = "veggie.jpg", IsAvailable = true }
        );
    }
}


