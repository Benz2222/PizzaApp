using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace PizzaApp.BuildingBlocks.Swagger;

/// <summary>
/// Cấu hình Swagger dùng chung cho mọi service: đọc chú thích XML từ code,
/// và thêm nút "Authorize" để dán JWT ngay trên trang Swagger.
/// </summary>
public static class SwaggerExtensions
{
    /// <param name="serviceName">Tên service, hiện trên tiêu đề trang Swagger. VD: "Auth".</param>
    /// <param name="description">Mô tả ngắn service làm gì.</param>
    public static IServiceCollection AddPizzaSwagger(
        this IServiceCollection services, string serviceName, string description)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = $"PizzaApp — {serviceName} API",
                Version = "v1",
                Description = description
            });

            // Nạp chú thích /// <summary> từ file XML do build sinh ra.
            // Cần <GenerateDocumentationFile>true</GenerateDocumentationFile> trong .csproj.
            var xmlFile = $"{Assembly.GetEntryAssembly()!.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);

            // Nút Authorize: dán token vào là gọi được các API [Authorize].
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Dán token lấy từ POST /api/auth/login (chỉ dán token, không cần chữ 'Bearer')."
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}
