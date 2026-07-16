using PizzaApp.Auth.Core.Entities;
using PizzaApp.Auth.Infrastructure.Services;
using Xunit;

namespace PizzaApp.Auth.Tests;

public class AuthServiceTests
{
    [Fact]
    public void ValidateResetToken_ValidTokenWithinExpiry_ReturnsNull()
    {
        var user = new User
        {
            ResetPasswordToken = "abc",
            ResetPasswordTokenExpiry = DateTime.UtcNow.AddMinutes(10)
        };

        var error = AuthService.ValidateResetToken(user, "abc");

        Assert.Null(error);
    }

    [Fact]
    public void ValidateResetToken_WrongToken_ReturnsError()
    {
        var user = new User
        {
            ResetPasswordToken = "abc",
            ResetPasswordTokenExpiry = DateTime.UtcNow.AddMinutes(10)
        };

        var error = AuthService.ValidateResetToken(user, "wrong");

        Assert.Equal("Token không hợp lệ!", error);
    }

    [Fact]
    public void ValidateResetToken_Expired_ReturnsError()
    {
        var user = new User
        {
            ResetPasswordToken = "abc",
            ResetPasswordTokenExpiry = DateTime.UtcNow.AddMinutes(-1)
        };

        var error = AuthService.ValidateResetToken(user, "abc");

        Assert.Equal("Token đã hết hạn! Vui lòng yêu cầu lại.", error);
    }
}
