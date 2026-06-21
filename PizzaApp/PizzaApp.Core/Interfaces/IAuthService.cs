using PizzaApp.Core.DTOs.Auth;

namespace PizzaApp.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto?> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto?> LoginAsync(LoginDto dto);
    Task<string> ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(ResetPasswordDto dto);
}
