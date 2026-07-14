namespace PizzaApp.BuildingBlocks.Auth;

public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "PizzaApp";
    public string Audience { get; set; } = "PizzaAppUsers";
    public int ExpiresInDays { get; set; } = 7;
}
