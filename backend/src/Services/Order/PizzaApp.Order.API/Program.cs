using PizzaApp.Order.Core.Interfaces;
using PizzaApp.BuildingBlocks.Swagger;
using PizzaApp.Order.Infrastructure;
using PizzaApp.Order.Infrastructure.Clients;
using PizzaApp.Order.Infrastructure.Services;
using PizzaApp.BuildingBlocks.Auth;
using PizzaApp.BuildingBlocks.Mongo;
using PizzaApp.BuildingBlocks.Messaging;

var builder = WebApplication.CreateBuilder(args);

var mongoSettings = new MongoSettings();
builder.Configuration.GetSection("MongoDB").Bind(mongoSettings);
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);
var busSettings = new EventBusSettings();
builder.Configuration.GetSection("EventBus").Bind(busSettings);

builder.Services.AddSingleton(mongoSettings);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<OrderDbContext>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddRabbitMqEventBus(busSettings);

builder.Services.AddHttpContextAccessor();
var productUrl = builder.Configuration["Services:ProductUrl"] ?? "http://localhost:5002/";
var paymentUrl = builder.Configuration["Services:PaymentUrl"] ?? "http://localhost:5006/";
var cartUrl = builder.Configuration["Services:CartUrl"] ?? "http://localhost:5004/";
builder.Services.AddHttpClient<IProductClient, ProductHttpClient>(c => { c.BaseAddress = new Uri(productUrl); c.Timeout = TimeSpan.FromSeconds(5); });
builder.Services.AddHttpClient<IPaymentClient, PaymentHttpClient>(c => { c.BaseAddress = new Uri(paymentUrl); c.Timeout = TimeSpan.FromSeconds(10); });
builder.Services.AddHttpClient<ICartClient, CartHttpClient>(c => { c.BaseAddress = new Uri(cartUrl); c.Timeout = TimeSpan.FromSeconds(5); });

// Consumer: PaymentSucceeded -> ConfirmPayment
builder.Services.AddRabbitMqConsumer<PaymentSucceededEvent>("order.payment-succeeded", async (sp, evt) =>
{
    var svc = sp.GetRequiredService<IOrderService>();
    await svc.ConfirmPaymentAsync(evt.OrderId);
});

builder.Services.AddPizzaJwtAuthentication(jwtSettings);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddPizzaSwagger("Order", "Dat hang, vong doi don, thong ke. Goi REST sang Product/Payment/Cart; nghe event PaymentSucceeded.");
builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
