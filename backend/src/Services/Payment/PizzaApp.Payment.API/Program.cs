using PizzaApp.Payment.Core;
using PizzaApp.Payment.Core.Interfaces;
using PizzaApp.Payment.Infrastructure;
using PizzaApp.Payment.Infrastructure.Gateways;
using PizzaApp.Payment.Infrastructure.Services;
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
var paymentSettings = new PaymentSettings();
builder.Configuration.GetSection("Payment").Bind(paymentSettings);

builder.Services.AddSingleton(mongoSettings);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(paymentSettings);
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<PaymentDbContext>();

// Chọn cổng thanh toán: "PayOS" = tiền thật, còn lại = Mock (giả lập, demo offline)
var provider = builder.Configuration["Payment:Provider"] ?? "Mock";
if (provider.Equals("PayOS", StringComparison.OrdinalIgnoreCase))
{
    var payOsSettings = new PaymentSettingsPayOS();
    builder.Configuration.GetSection("PayOS").Bind(payOsSettings);
    builder.Services.AddSingleton(payOsSettings);
    builder.Services.AddSingleton(new Net.payOS.PayOS(
        payOsSettings.ClientId, payOsSettings.ApiKey, payOsSettings.ChecksumKey));
    builder.Services.AddSingleton<IPaymentGateway, PayOSPaymentGateway>();
    Console.WriteLine("[Payment] Dùng PayOS (TIỀN THẬT)");
}
else
{
    builder.Services.AddSingleton<IPaymentGateway, MockPaymentGateway>();
    Console.WriteLine("[Payment] Dùng MockPaymentGateway (giả lập)");
}

builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddRabbitMqEventBus(busSettings);

builder.Services.AddPizzaJwtAuthentication(jwtSettings);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
