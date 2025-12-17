namespace backend.Models.DTOs
{
    public class AuthResponseDto
    {
        public string? UserId { get; set; }
        public string? TenantId { get; set; }
        public string? Email { get; set; }
        public string? Token { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public string? Message { get; set; }
        public bool Success { get; set; }
    }
}

