using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using PizzaApp.Core.DTOs.Auth;
using PizzaApp.Core.Entities;
using PizzaApp.Core.Interfaces;
using PizzaApp.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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

    public async Task<AuthResponseDto?> RegisterAsync(RegisterDto dto)
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

        return new AuthResponseDto
        {
            Token = GenerateToken(user),
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber
        };
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
    {
        var user = await _users.Find(u => u.Email == dto.Email).FirstOrDefaultAsync();
        if (user == null) return null;

        var isValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        if (!isValid) return null;

        return new AuthResponseDto
        {
            Token = GenerateToken(user),
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber
        };
    }

    /// <summary>
    /// Bước 1: Tạo token reset password và lưu vào DB.
    /// Trong thực tế, token này sẽ được gửi qua email.
    /// </summary>
    public async Task<string> ForgotPasswordAsync(string email)
    {
        var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (user is null)
            throw new InvalidOperationException("Email không tồn tại trong hệ thống!");

        // Tạo token ngẫu nhiên an toàn
        var resetToken = GenerateSecureToken();

        // Token hết hạn sau 15 phút
        var update = Builders<User>.Update
            .Set(u => u.ResetPasswordToken, resetToken)
            .Set(u => u.ResetPasswordTokenExpiry, DateTime.UtcNow.AddMinutes(15));

        await _users.UpdateOneAsync(u => u.Id == user.Id, update);

        // Trong thực tế: gửi email chứa resetToken cho user
        // Ở đây trả về token trực tiếp để test (production phải gửi email)
        return resetToken;
    }

    /// <summary>
    /// Bước 2: Xác thực token và đặt mật khẩu mới.
    /// </summary>
    public async Task ResetPasswordAsync(ResetPasswordDto dto)
    {
        var user = await _users.Find(u => u.Email == dto.Email).FirstOrDefaultAsync();
        if (user is null)
            throw new InvalidOperationException("Email không tồn tại trong hệ thống!");

        // Kiểm tra token có khớp không
        if (user.ResetPasswordToken != dto.Token)
            throw new InvalidOperationException("Token không hợp lệ!");

        // Kiểm tra token có hết hạn chưa
        if (user.ResetPasswordTokenExpiry is null || user.ResetPasswordTokenExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("Token đã hết hạn! Vui lòng yêu cầu lại.");

        // Cập nhật mật khẩu mới và xóa token
        var newHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        var update = Builders<User>.Update
            .Set(u => u.PasswordHash, newHash)
            .Set(u => u.ResetPasswordToken, (string?)null)
            .Set(u => u.ResetPasswordTokenExpiry, (DateTime?)null);

        await _users.UpdateOneAsync(u => u.Id == user.Id, update);
    }

    private string GenerateToken(User user)
    {
        var jwtSettings = _config.GetSection("JwtSettings");
        var secretKeyValue = jwtSettings["SecretKey"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKeyValue));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
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

    /// <summary>
    /// Tạo token ngẫu nhiên 64 ký tự (hex) để dùng làm reset password token.
    /// </summary>
    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLower();
    }
}
