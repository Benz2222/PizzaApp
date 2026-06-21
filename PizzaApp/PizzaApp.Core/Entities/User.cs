using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PizzaApp.Core.Entities
{
    public class User
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? ResetPasswordToken { get; set; }
        public DateTime? ResetPasswordTokenExpiry { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
