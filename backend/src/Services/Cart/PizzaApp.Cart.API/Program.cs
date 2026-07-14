using PizzaApp.Cart.Core.Interfaces;
using PizzaApp.Cart.Infrastructure;
using PizzaApp.Cart.Infrastructure.Clients;
using PizzaApp.Cart.Infrastructure.Services;
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
builder.Services.AddSingleton<CartDbContext>();
builder.Services.AddScoped<ICartService, CartService>();

var productUrl = builder.Configuration["Services:ProductUrl"] ?? "http://localhost:5002/";
builder.Services.AddHttpClient<IProductClient, ProductHttpClient>(c =>
{
    c.BaseAddress = new Uri(productUrl);
    c.Timeout = TimeSpan.FromSeconds(5);
});

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
