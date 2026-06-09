using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PizzaApp.Infrastructure.Data;
using MongoDB.Bson;
using PizzaApp.Core.Entities;

namespace PizzaApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DbTestController : ControllerBase
    {
        private readonly MongoDbService _mongoService;

        public DbTestController(MongoDbService mongoService)
        {
            _mongoService = mongoService;
        }

        [HttpGet("mongodb")]
        public async Task<IActionResult> TestMongoConnection()
        {
            try
            {
                var db = _mongoService.GetCollection<BsonDocument>("test").Database;
                await db.RunCommandAsync((Command<BsonDocument>)"{ping:1}");

                return Ok(new {
                    status = "Success",
                    message = "Kết nối MongoDB thành công!",
                    database = db.DatabaseNamespace.DatabaseName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new {
                    status = "Error",
                    message = "Kết nối MongoDB thất bại!",
                    error = ex.Message
                });
            }
        }

        [HttpPost("seed-products")]
        public async Task<IActionResult> SeedProducts()
        {
            var collection = _mongoService.GetCollection<Product>("Products");

            // Xóa hết sản phẩm cũ
            await collection.DeleteManyAsync(_ => true);

            // Tạo sản phẩm mới với ID đơn giản "1", "2", "3", "4"
            var products = new List<Product>
            {
                new Product { Id = "1", Name = "Margherita", Description = "Phô mai, cà chua, húng quế", Price = 89000, Category = "Truyền thống", ImageUrl = "margherita.jpg", IsAvailable = true },
                new Product { Id = "2", Name = "Hải Sản", Description = "Tôm, mực, sốt tỏi bơ", Price = 129000, Category = "Hải sản", ImageUrl = "seafood.jpg", IsAvailable = true },
                new Product { Id = "3", Name = "BBQ Bò", Description = "Thịt bò, hành tây, sốt BBQ", Price = 119000, Category = "Đặc biệt", ImageUrl = "bbq.jpg", IsAvailable = true },
                new Product { Id = "4", Name = "Veggie", Description = "Rau củ, nấm, phô mai", Price = 79000, Category = "Chay", ImageUrl = "veggie.jpg", IsAvailable = true }
            };

            await collection.InsertManyAsync(products);

            return Ok(new { message = "Đã seed 4 sản phẩm với ID 1, 2, 3, 4 vào MongoDB!", products });
        }
    }
}
