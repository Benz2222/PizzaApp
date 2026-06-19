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

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var token = await _authService.RegisterAsync(dto);
        if (token == null)
            return BadRequest(new { message = "Email đã được sử dụng!" });

        return Ok(new { token });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var token = await _authService.LoginAsync(dto);
        if (token == null)
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng!" });

        return Ok(new { token });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        try
        {
            await _authService.ResetPasswordAsync(dto.Email, dto.NewPassword);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(new { message = "Đổi mật khẩu thành công!" });
    }
}
