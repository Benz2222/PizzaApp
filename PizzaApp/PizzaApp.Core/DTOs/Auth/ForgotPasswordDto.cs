using System.ComponentModel.DataAnnotations;

namespace PizzaApp.Core.DTOs.Auth;

/// <summary>
/// Bước 1: User gửi email để yêu cầu reset password.
/// Server sẽ tạo token và lưu vào DB (trong thực tế sẽ gửi email).
/// </summary>
public class ForgotPasswordDto
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;
}
