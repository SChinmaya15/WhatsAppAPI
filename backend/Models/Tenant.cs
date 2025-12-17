using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace backend.Models
{
    public class Tenant
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        
        [BsonRequired]
        public string Name { get; set; } = string.Empty;
        
        public string? Domain { get; set; }
        public string? Description { get; set; }
        
        [BsonRepresentation(BsonType.ObjectId)]
        public string? OwnerId { get; set; } // User ID of the tenant owner
        
        public Dictionary<string, object>? Settings { get; set; }
        
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        
        public bool IsActive { get; set; } = true;
    }
}

