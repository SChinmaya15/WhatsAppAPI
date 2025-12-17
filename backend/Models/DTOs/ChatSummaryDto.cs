using System.Text.Json.Serialization;

namespace backend.Models.DTOs
{
    public class ChatSummaryDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("lastMessage")]
        public string? LastMessage { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTimeOffset? Timestamp { get; set; }

        [JsonPropertyName("lastDirection")]
        public string? LastDirection { get; set; }

        [JsonPropertyName("unreadCount")]
        public int UnreadCount { get; set; }
    }
}

