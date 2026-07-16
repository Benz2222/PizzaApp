using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using PizzaApp.BuildingBlocks.Auth;
using Xunit;

namespace PizzaApp.BuildingBlocks.Tests;

public class JwtTokenGeneratorTests
{
    private static JwtSettings Settings() => new()
    {
        SecretKey = "PizzaAppSuperSecretKey2024_DoNotShare!",
        Issuer = "PizzaApp",
        Audience = "PizzaAppUsers",
        ExpiresInDays = 7
    };

    [Fact]
    public void Generate_EmbedsUserIdEmailAndRoleClaims()
    {
        var gen = new JwtTokenGenerator(Settings());

        var token = gen.Generate("user-123", "a@b.com", "Nguyen Van A", "Customer");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("PizzaApp", jwt.Issuer);
        Assert.Contains(jwt.Audiences, a => a == "PizzaAppUsers");
        Assert.Equal("user-123", jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("a@b.com", jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
        Assert.Equal("Customer", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void Generate_SetsExpiryFromSettings()
    {
        var gen = new JwtTokenGenerator(Settings());

        var token = gen.Generate("u", "e", "n", "Customer");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.True(jwt.ValidTo > DateTime.UtcNow.AddDays(6));
        Assert.True(jwt.ValidTo < DateTime.UtcNow.AddDays(8));
    }
}
