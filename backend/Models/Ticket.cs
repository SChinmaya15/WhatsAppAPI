using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace backend.Models
{
    public class Ticket
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        
        public string TicketNumber { get; set; } = string.Empty; // Auto-generated unique ticket number
        
        // Display / contact fields
        public string? ContactName { get; set; }
        public string? ContactEmail { get; set; }
        public string? Source { get; set; } // WEBSITE, SOCIAL, OTHER, etc.
        public string? Budget { get; set; }
        public string? Stage { get; set; } = "New"; // New, Contacted, Qualified, Proposal, Negotiation, Booked
        public int PriorityScore { get; set; } = 0; // numeric priority for UI bars

        [BsonRequired]
        public string Subject { get; set; } = string.Empty;
        
        [BsonRequired]
        public string Description { get; set; } = string.Empty;
        
        public string Status { get; set; } = "Open"; // Open, InProgress, Resolved, Closed
        
        public string Priority { get; set; } = "Medium"; // Low, Medium, High, Urgent
        
        // Customer/User information
        public string? CustomerPhoneNumber { get; set; } // WhatsApp phone number
        
        [BsonRepresentation(BsonType.ObjectId)]
        public string? CustomerId { get; set; } // User ID if customer is registered
        
        
        [BsonRepresentation(BsonType.ObjectId)]
        public string? TenantId { get; set; } // Associated tenant
        
        [BsonRepresentation(BsonType.ObjectId)]
        public string? AssignedTo { get; set; } // User ID of assigned agent/admin
        
        // Message reference
        [BsonRepresentation(BsonType.ObjectId)]
        public string? InitialMessageId { get; set; } // Reference to the message that created this ticket
        
        public List<string> MessageIds { get; set; } = new(); // All messages associated with this ticket
        
        // Metadata
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? ResolvedAt { get; set; }
        public DateTimeOffset? ClosedAt { get; set; }
        
        public Dictionary<string, object>? Metadata { get; set; } // Additional flexible data
    }
}

