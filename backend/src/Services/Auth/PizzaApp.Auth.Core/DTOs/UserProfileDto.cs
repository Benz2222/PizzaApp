namespace PizzaApp.Auth.Core.DTOs;

public class UserProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class AuthStatsDto
{
    public int TotalUsers { get; set; }
    public Dictionary<string, int> ByRole { get; set; } = new();
}
