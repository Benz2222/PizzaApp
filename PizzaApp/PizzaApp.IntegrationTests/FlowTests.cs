using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Mongo2Go;
using MongoDB.Driver;
using PizzaApp.Core.DTOs.Auth;
using PizzaApp.Core.DTOs.Cart;
using PizzaApp.Core.DTOs.Category;
using PizzaApp.Core.DTOs.Product;
using PizzaApp.Core.DTOs.Order;
using PizzaApp.Core.Entities;
using PizzaApp.Infrastructure.Data;
using PizzaApp.Infrastructure.Services;
using Xunit;

namespace PizzaApp.IntegrationTests;

public class FlowTests : IDisposable
{
    private readonly MongoDbRunner _runner;
    private readonly IConfiguration _configuration;
    private readonly MongoDbService _mongoDb;
    private readonly AuthService _authService;
    private readonly ProductService _productService;
    private readonly CartService _cartService;
    private readonly OrderService _orderService;

    public FlowTests()
    {
        // Start ephemeral MongoDB
        _runner = MongoDbRunner.Start(singleNodeReplSet: true);

        // Build configuration (Mongo + Jwt minimal settings)
        var settings = new Dictionary<string, string>
        {
            ["MongoDB:ConnectionString"] = _runner.ConnectionString,
            ["MongoDB:DatabaseName"] = "PizzaAppTestDb",
            ["JwtSettings:SecretKey"] = "TestSecretKeyForIntegrationTestsDontUse",
            ["JwtSettings:Issuer"] = "PizzaAppTest",
            ["JwtSettings:Audience"] = "PizzaAppTestUsers",
            ["JwtSettings:ExpiresInDays"] = "7",
            // PayOS keys (not used - OrderService handles failures)
            ["PayOS:ClientId"] = "",
            ["PayOS:ApiKey"] = "",
            ["PayOS:ChecksumKey"] = ""
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        // Create MongoDbService and services under test
        _mongoDb = new MongoDbService(_configuration);
        _authService = new AuthService(_mongoDb, _configuration);
        _productService = new ProductService(_mongoDb);
        _cartService = new CartService(_mongoDb);
        // instantiate PayOS with empty keys (OrderService wraps calls in try/catch)
        var payOsInstance = new Net.payOS.PayOS(_configuration["PayOS:ClientId"]!, _configuration["PayOS:ApiKey"]!, _configuration["PayOS:ChecksumKey"]!);
        _orderService = new OrderService(_mongoDb, payOsInstance);
    }

    [Fact(DisplayName = "Full flow: register -> login -> add to cart -> checkout -> confirm payment")]
    public async Task FullFlow_RegisterLoginCartCheckoutConfirmPayment()
    {
        // 1) Register user
        var registerDto = new RegisterDto
        {
            FullName = "Integration Tester",
            Email = "itest@example.com",
            Password = "Secret123!",
            PhoneNumber = "0123456789"
        };

        var token = await _authService.RegisterAsync(registerDto);
        Assert.False(string.IsNullOrEmpty(token));

        // Retrieve created user from DB to get Id
        var users = _mongoDb.GetCollection<User>("Users");
        var user = await users.Find(u => u.Email == registerDto.Email).FirstOrDefaultAsync();
        Assert.NotNull(user);
        var userId = user!.Id;
        Assert.False(string.IsNullOrEmpty(userId));

        // 2) Create a Category and Product
        var categories = _mongoDb.GetCollection<Category>("Categories");
        var category = new Category { Name = "Classic" };
        await categories.InsertOneAsync(category);

        var createProductDto = new CreateProductDTO
        {
            Name = "Margherita",
            Description = "Test pizza",
            Price = 120,
            ImageUrl = "http://example.com/pizza.jpg",
            CategoryId = category.Id
        };

        var productDto = await _productService.CreateAsync(createProductDto);
        Assert.NotNull(productDto);
        Assert.False(string.IsNullOrEmpty(productDto.Id));

        // 3) Add to cart
        var cartItemDto = new CartItemDto
        {
            ProductId = productDto.Id,
            Quantity = 2,
            Size = "Large"
        };

        await _cart_service_AddAndVerify(userId, cartItemDto);

        // 4) Checkout -> create order from cart
        var cartItems = await _cartService.GetCartAsync(userId);
        Assert.NotEmpty(cartItems);

        var createOrderDto = new CreateOrderDto
        {
            DeliveryAddress = "123 Test St",
            Items = cartItems.ConvertAll(c => new OrderItemDto
            {
                ProductId = c.ProductId,
                Quantity = c.Quantity,
                Size = c.Size
            })
        };

        var orderId = await _orderService.CreateOrderAsync(userId, createOrderDto);
        Assert.False(string.IsNullOrEmpty(orderId));

        // Verify order stored with Unpaid payment status
        var orders = _mongoDb.GetCollection<Order>("Orders");
        var order = await orders.Find(o => o.Id == orderId).FirstOrDefaultAsync();
        Assert.NotNull(order);
        Assert.Equal("Unpaid", order!.PaymentStatus);

        // 5) Simulate webhook effect by calling ConfirmPaymentAsync
        var confirmResult = await _order_service_ConfirmAndVerify(orderId);
        Assert.True(confirmResult);

        var updatedOrder = await orders.Find(o => o.Id == orderId).FirstOrDefaultAsync();
        Assert.NotNull(updatedOrder);
        Assert.Equal("Paid", updatedOrder!.PaymentStatus);
    }

    private async Task _cart_service_AddAndVerify(string userId, CartItemDto cartItemDto)
    {
        await _cartService.AddToCartAsync(userId, cartItemDto);
        var cart = await _cartService.GetCartAsync(userId);
        Assert.Single(cart);
        Assert.Equal(cartItemDto.ProductId, cart[0].ProductId);
        Assert.Equal(cartItemDto.Quantity, cart[0].Quantity);
    }

    private async Task<bool> _order_service_ConfirmAndVerify(string orderId)
    {
        var payments = _mongoDb.GetCollection<Payment>("Payments");
        // If Payment record exists, ConfirmPaymentAsync will update it; otherwise it still updates order.
        await Task.Delay(50); // small delay for DB consistency in tests
        return await _orderService.ConfirmPaymentAsync(orderId);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }
}