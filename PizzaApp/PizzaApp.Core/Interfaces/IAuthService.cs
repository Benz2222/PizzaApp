using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PizzaApp.Core.DTOs.Auth;

namespace PizzaApp.Core.Interfaces;

public interface IAuthService
{
    Task<string?> RegisterAsync(RegisterDto dto);  // trả về JWT token
    Task<string?> LoginAsync(LoginDto dto);         // trả về JWT token
    Task ResetPasswordAsync(string email, string newPassword);
}
