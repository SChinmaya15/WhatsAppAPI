using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace backend.Models
{
    public class MessageRecord
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }   // use Id instead of _id
        public string? To { get; set; }        // USER BUSINESS ID
        public string? From { get; set; }      // SERVICE PROVIDER
        public string? Body { get; set; }
        public bool Incoming { get; set; }
        public string? Status { get; set; } // sent/delivered/read/failed
        public string MetaMessageId { get; set; } // id returned by Meta for outbound messages
        public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
