using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace backend.Models
{
    public class Customer
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRequired]
        public string Name { get; set; } = string.Empty;

        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Source { get; set; }
        public string? CustomerId { get; set; } // External/Client customer identifier

        [BsonRepresentation(BsonType.ObjectId)]
        public string? TenantId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public Dictionary<string, object>? Metadata { get; set; }
    }
}

