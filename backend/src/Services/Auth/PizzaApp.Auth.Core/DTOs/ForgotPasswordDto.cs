using System.ComponentModel.DataAnnotations;

namespace PizzaApp.Auth.Core.DTOs;

public class ForgotPasswordDto
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;
}
