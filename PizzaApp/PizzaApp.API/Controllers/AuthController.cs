using Microsoft.AspNetCore.Mvc;
using PizzaApp.Core.DTOs.Auth;
using PizzaApp.Core.Interfaces;

namespace PizzaApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Đăng ký tài khoản mới. Trả về token + thông tin user.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto);
        if (result == null)
            return BadRequest(new { message = "Email đã được sử dụng!" });

        return Ok(new { message = "Đăng ký thành công!", data = result });
    }

    /// <summary>
    /// Đăng nhập. Trả về token + thông tin user.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (result == null)
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng!" });

        return Ok(new { message = "Đăng nhập thành công!", data = result });
    }

    /// <summary>
    /// Bước 1: Yêu cầu reset password.
    /// Gửi email để server tạo token (trả về token để test).
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        try
        {
            var token = await _authService.ForgotPasswordAsync(dto.Email);
            // Trong production: token sẽ được gửi qua email, không trả về đây
            return Ok(new
            {
                message = "Token reset password đã được tạo. Vui lòng kiểm tra email.",
                // TODO: Ở production, dòng dưới phải xóa — token chỉ gửi qua email
                token = token
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Bước 2: Đặt mật khẩu mới bằng token từ email.
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        try
        {
            await _authService.ResetPasswordAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(new { message = "Đổi mật khẩu thành công!" });
    }
}
