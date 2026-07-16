using PizzaApp.Auth.Core.DTOs;

namespace PizzaApp.Auth.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto?> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto?> LoginAsync(LoginDto dto);
    /// <summary>Tạo mã đặt lại và ghi ra log server. KHÔNG trả mã về client —
    /// trả mã trong response sẽ cho phép bất kỳ ai chiếm tài khoản chỉ từ email.</summary>
    Task ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(ResetPasswordDto dto);
    Task<UserProfileDto?> GetProfileAsync(string userId);
    Task<AuthStatsDto> GetStatsAsync();
}
