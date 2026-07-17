using PizzaApp.Payment.Core;
using PizzaApp.BuildingBlocks.Swagger;
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
builder.Services.AddPizzaSwagger("Payment", "Tao giao dich + QR (PayOS hoac Mock), nhan webhook, ban event PaymentSucceeded.");
builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Tự đăng ký webhook với PayOS sau khi server đã lắng nghe.
// PayOS sẽ gọi thử URL này -> phải public (ngrok/VPS) và trả 200.
if (provider.Equals("PayOS", StringComparison.OrdinalIgnoreCase))
{
    app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
    {
        var hookUrl = $"{paymentSettings.PublicBaseUrl.TrimEnd('/')}/api/payment/webhook";
        if (hookUrl.StartsWith("http://localhost") || hookUrl.StartsWith("http://10.0.2.2"))
        {
            Console.WriteLine($"[Payment] Bỏ qua đăng ký webhook: '{hookUrl}' không public. Cần ngrok/VPS.");
            return;
        }
        var payOs = app.Services.GetRequiredService<Net.payOS.PayOS>();
        for (var i = 1; i <= 3; i++)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3)); // chờ server sẵn sàng nhận probe
                await payOs.confirmWebhook(hookUrl);
                Console.WriteLine($"[Payment] Đã đăng ký webhook PayOS: {hookUrl}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Payment] Đăng ký webhook lỗi (lần {i}/3): {ex.Message}");
            }
        }
        Console.WriteLine("[Payment] KHÔNG đăng ký được webhook -> phải khai tay tại my.payos.vn");
    }));
}

app.Run();
