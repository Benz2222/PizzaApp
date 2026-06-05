using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PizzaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Category", "Description", "ImageUrl", "IsAvailable", "Name", "Price" },
                values: new object[,]
                {
                    { 1, "Truyền thống", "Phô mai, cà chua, húng quế", "margherita.jpg", true, "Margherita", 89000m },
                    { 2, "Hải sản", "Tôm, mực, sốt tỏi bơ", "seafood.jpg", true, "Hải Sản", 129000m },
                    { 3, "Đặc biệt", "Thịt bò, hành tây, sốt BBQ", "bbq.jpg", true, "BBQ Bò", 119000m },
                    { 4, "Chay", "Rau củ, nấm, phô mai", "veggie.jpg", true, "Veggie", 79000m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4);
        }
    }
}
