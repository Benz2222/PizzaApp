using PizzaApp.Auth.Core.DTOs;

namespace PizzaApp.Auth.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto?> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto?> LoginAsync(LoginDto dto);
    Task<string> ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(ResetPasswordDto dto);
    Task<UserProfileDto?> GetProfileAsync(string userId);
    Task<AuthStatsDto> GetStatsAsync();
}
