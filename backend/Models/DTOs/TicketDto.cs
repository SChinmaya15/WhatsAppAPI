using System.Text.Json.Serialization;

namespace backend.Models.DTOs
{
    public class TicketDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("ticketNumber")]
        public string TicketNumber { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "Open";

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        [JsonPropertyName("budget")]
        public string? Budget { get; set; }

        [JsonPropertyName("created")]
        public DateTimeOffset Created { get; set; }

        [JsonPropertyName("stage")]
        public string Stage { get; set; } = "New";

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}

