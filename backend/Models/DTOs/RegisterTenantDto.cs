using System.ComponentModel.DataAnnotations;

namespace backend.Models.DTOs
{
    public class RegisterTenantDto
    {
        [Required]
        [MinLength(2)]
        public string Name { get; set; } = string.Empty;
        
        public string? Domain { get; set; }
        public string? Description { get; set; }
        
        // Owner registration details (creates owner user along with tenant)
        [Required]
        [EmailAddress]
        public string OwnerEmail { get; set; } = string.Empty;
        
        [Required]
        [MinLength(6)]
        public string OwnerPassword { get; set; } = string.Empty;
        
        public string? OwnerFirstName { get; set; }
        public string? OwnerLastName { get; set; }
    }
}

