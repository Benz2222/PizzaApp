using System.Security.Cryptography;
using MongoDB.Driver;
using PizzaApp.Auth.Core.DTOs;
using PizzaApp.Auth.Core.Entities;
using PizzaApp.Auth.Core.Interfaces;
using PizzaApp.BuildingBlocks.Auth;

namespace PizzaApp.Auth.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IMongoCollection<User> _users;
    private readonly JwtTokenGenerator _tokens;

    public AuthService(AuthDbContext db, JwtTokenGenerator tokens)
    {
        _users = db.Users;
        _tokens = tokens;
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
        return ToAuthResponse(user);
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
    {
        var user = await _users.Find(u => u.Email == dto.Email).FirstOrDefaultAsync();
        if (user == null) return null;
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash)) return null;
        return ToAuthResponse(user);
    }

    public async Task<string> ForgotPasswordAsync(string email)
    {
        var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (user is null)
            throw new InvalidOperationException("Email không tồn tại trong hệ thống!");

        var resetToken = GenerateSecureToken();
        var update = Builders<User>.Update
            .Set(u => u.ResetPasswordToken, resetToken)
            .Set(u => u.ResetPasswordTokenExpiry, DateTime.UtcNow.AddMinutes(15));
        await _users.UpdateOneAsync(u => u.Id == user.Id, update);
        return resetToken;
    }

    public async Task ResetPasswordAsync(ResetPasswordDto dto)
    {
        var user = await _users.Find(u => u.Email == dto.Email).FirstOrDefaultAsync();
        if (user is null)
            throw new InvalidOperationException("Email không tồn tại trong hệ thống!");

        var error = ValidateResetToken(user, dto.Token);
        if (error is not null)
            throw new InvalidOperationException(error);

        var newHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        var update = Builders<User>.Update
            .Set(u => u.PasswordHash, newHash)
            .Set(u => u.ResetPasswordToken, (string?)null)
            .Set(u => u.ResetPasswordTokenExpiry, (DateTime?)null);
        await _users.UpdateOneAsync(u => u.Id == user.Id, update);
    }

    public async Task<UserProfileDto?> GetProfileAsync(string userId)
    {
        var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null) return null;
        return new UserProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            Role = user.Role
        };
    }

    /// <summary>Kiểm token reset. Trả null nếu hợp lệ, ngược lại trả message lỗi.</summary>
    public static string? ValidateResetToken(User user, string token)
    {
        if (user.ResetPasswordToken != token)
            return "Token không hợp lệ!";
        if (user.ResetPasswordTokenExpiry is null || user.ResetPasswordTokenExpiry < DateTime.UtcNow)
            return "Token đã hết hạn! Vui lòng yêu cầu lại.";
        return null;
    }

    private AuthResponseDto ToAuthResponse(User user) => new()
    {
        Token = _tokens.Generate(user.Id, user.Email, user.FullName, user.Role),
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber
    };

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLower();
    }
}
