using PizzaApp.Product.Core.Interfaces;
using PizzaApp.Product.Infrastructure;
using PizzaApp.Product.Infrastructure.Clients;
using PizzaApp.Product.Infrastructure.Services;
using PizzaApp.BuildingBlocks.Auth;
using PizzaApp.BuildingBlocks.Mongo;

var builder = WebApplication.CreateBuilder(args);

var mongoSettings = new MongoSettings();
builder.Configuration.GetSection("MongoDB").Bind(mongoSettings);
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);

builder.Services.AddSingleton(mongoSettings);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<ProductDbContext>();
builder.Services.AddScoped<IProductService, ProductService>();

// REST client tới Category service (địa chỉ đọc từ config "Services:CategoryUrl")
var categoryUrl = builder.Configuration["Services:CategoryUrl"] ?? "http://localhost:5003/";
builder.Services.AddHttpClient<ICategoryClient, CategoryHttpClient>(c =>
{
    c.BaseAddress = new Uri(categoryUrl);
    c.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddPizzaJwtAuthentication(jwtSettings);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// Phải tạo wwwroot/uploads TRƯỚC khi Build(): nếu thư mục chưa tồn tại thì
// WebRootPath = null và UseStaticFiles() sẽ trả 404 cho mọi ảnh.
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads"));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.UseStaticFiles(); // phục vụ /uploads/*
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
