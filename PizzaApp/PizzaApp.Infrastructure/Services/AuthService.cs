using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using PizzaApp.Core.DTOs.Auth;
using PizzaApp.Core.Entities;
using PizzaApp.Core.Interfaces;
using PizzaApp.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PizzaApp.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IMongoCollection<User> _users;
    private readonly IConfiguration _config;

    public AuthService(MongoDbService mongoDb, IConfiguration config)
    {
        _users = mongoDb.GetCollection<User>("Users");
        _config = config;
    }

    public async Task<string?> RegisterAsync(RegisterDto dto)
    {
        var exists = await _users.Find(u => u.Email == dto.Email).AnyAsync();
        if (exists) return null;

        var user = new User
        {
            FullName = dto.FullName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        await _users.InsertOneAsync(user);

        return GenerateToken(user);
    }

    public async Task<string?> LoginAsync(LoginDto dto)
    {
        var user = await _users.Find(u => u.Email == dto.Email).FirstOrDefaultAsync();
        if (user == null) return null;

        var isValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        if (!isValid) return null;

        return GenerateToken(user);
    }

    public async Task ResetPasswordAsync(string email, string newPassword)
    {
        var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (user is null)
            throw new InvalidOperationException("Email không tồn tại!");

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

        await _users.UpdateOneAsync(
            u => u.Id == user.Id,
            Builders<User>.Update.Set(u => u.PasswordHash, newHash)
        );
    }

    private string GenerateToken(User user)
    {
        var jwtSettings = _config.GetSection("JwtSettings");
        var secretKeyValue = jwtSettings["SecretKey"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKeyValue));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id), // Id giờ đã là string
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName)
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(int.Parse(jwtSettings["ExpiresInDays"] ?? "7")),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
