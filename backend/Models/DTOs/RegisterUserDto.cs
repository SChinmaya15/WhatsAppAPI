using System.ComponentModel.DataAnnotations;

namespace backend.Models.DTOs
{
    public class RegisterUserDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
        
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        
        public string? TenantId { get; set; } // Optional - for registering user to existing tenant
    }
}

