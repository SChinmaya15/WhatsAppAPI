using System.ComponentModel.DataAnnotations;

namespace backend.Models.DTOs
{
    public class AddCustomerDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        public string? Phone { get; set; }
        public string? Source { get; set; }
        public string? TenantId { get; set; }
        public string? CustomerId { get; set; }
    }
}

