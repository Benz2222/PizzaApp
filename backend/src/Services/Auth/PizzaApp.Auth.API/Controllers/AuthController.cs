using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PizzaApp.Auth.Core.DTOs;
using PizzaApp.Auth.Core.Interfaces;

namespace PizzaApp.Auth.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>Đăng ký tài khoản mới. Trả về JWT để đăng nhập luôn.</summary>
    /// <remarks>
    /// Mật khẩu được băm bằng BCrypt, không bao giờ lưu dạng thô.
    /// Tài khoản mới mặc định có Role = "Customer".
    ///
    /// Ví dụ request:
    ///
    ///     POST /api/auth/register
    ///     {
    ///       "fullName": "Trần Văn A",
    ///       "email": "a@gmail.com",
    ///       "password": "123456",
    ///       "phoneNumber": "0900000000"
    ///     }
    ///
    /// Ví dụ response:
    ///
    ///     {
    ///       "message": "Đăng ký thành công!",
    ///       "data": { "token": "eyJhbGci...", "id": "...", "fullName": "Trần Văn A" }
    ///     }
    /// </remarks>
    /// <response code="200">Đăng ký thành công, trả về token.</response>
    /// <response code="400">Email đã được dùng.</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto);
        if (result == null)
            return BadRequest(new { message = "Email đã được sử dụng!" });
        return Ok(new { message = "Đăng ký thành công!", data = result });
    }

    /// <summary>Đăng nhập bằng email + mật khẩu, nhận JWT.</summary>
    /// <remarks>
    /// Token trả về có hạn 7 ngày, chứa Id, Email và Role.
    /// Dán token vào nút **Authorize** ở đầu trang để gọi các API cần đăng nhập.
    ///
    /// Ví dụ request:
    ///
    ///     POST /api/auth/login
    ///     { "email": "admin@pizza.com", "password": "admin123" }
    /// </remarks>
    /// <response code="200">Đăng nhập thành công, trả về token.</response>
    /// <response code="401">Sai email hoặc mật khẩu.</response>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (result == null)
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng!" });
        return Ok(new { message = "Đăng nhập thành công!", data = result });
    }

    /// <summary>Lấy thông tin tài khoản đang đăng nhập (dựa vào token).</summary>
    /// <response code="200">Thông tin tài khoản.</response>
    /// <response code="401">Thiếu hoặc sai token.</response>
    /// <response code="404">Token hợp lệ nhưng tài khoản không còn tồn tại.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMe()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();
        var profile = await _authService.GetProfileAsync(userId);
        if (profile == null)
            return NotFound(new { message = "Không tìm thấy tài khoản." });
        return Ok(profile);
    }

    /// <summary>Thống kê người dùng theo Role, cho Dashboard admin (chỉ Admin).</summary>
    /// <response code="200">Tổng số user và số lượng theo từng Role.</response>
    /// <response code="403">Không phải Admin.</response>
    [HttpGet("admin/stats")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(AuthStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Stats() => Ok(await _authService.GetStatsAsync());

    /// <summary>Yêu cầu mã đặt lại mật khẩu.</summary>
    /// <remarks>
    /// **Bảo mật:** mã đặt lại KHÔNG được trả về trong response — nếu trả về thì bất kỳ ai
    /// biết email cũng chiếm được tài khoản. Mã chỉ được ghi ra log của service Auth
    /// (triển khai thật thì gửi qua email). Mã hết hạn sau 15 phút.
    ///
    /// Ví dụ request:
    ///
    ///     POST /api/auth/forgot-password
    ///     { "email": "a@gmail.com" }
    ///
    /// Ví dụ response:
    ///
    ///     { "message": "Mã đặt lại đã được tạo và gửi đi." }
    /// </remarks>
    /// <response code="200">Đã tạo mã.</response>
    /// <response code="400">Email không tồn tại.</response>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        try
        {
            await _authService.ForgotPasswordAsync(dto.Email);
            // Không trả mã về client: mã chỉ có trong log server (thật thì gửi qua email).
            return Ok(new { message = "Mã đặt lại đã được tạo và gửi đi." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Đặt lại mật khẩu bằng mã lấy từ forgot-password.</summary>
    /// <remarks>
    /// Ví dụ request:
    ///
    ///     POST /api/auth/reset-password
    ///     {
    ///       "email": "a@gmail.com",
    ///       "token": "65df3c41...",
    ///       "newPassword": "matkhaumoi"
    ///     }
    /// </remarks>
    /// <response code="200">Đổi mật khẩu thành công.</response>
    /// <response code="400">Mã sai, mã hết hạn, hoặc email không tồn tại.</response>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
