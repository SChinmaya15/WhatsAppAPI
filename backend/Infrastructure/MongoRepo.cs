using backend.Models;
using MongoDB.Driver;

namespace backend.Infrastructure
{
    public class MongoRepo
    {
        private readonly IMongoCollection<MessageRecord> _messages;
        public MongoRepo(IConfiguration cfg)
        {
            var client = new MongoClient(cfg["Mongo:ConnectionString"]);
            var db = client.GetDatabase(cfg["Mongo:Database"]);
            _messages = db.GetCollection<MessageRecord>("messages");
        }

        public Task CreateMessageAsync(MessageRecord m) => _messages.InsertOneAsync(m);
        public Task<List<MessageRecord>> GetConversationAsync(string userPhone, string businessNumber) =>
            _messages.Find(m => m.From == userPhone && m.To == businessNumber || m.From == businessNumber && m.To == userPhone)
                     .SortBy(m => m.ReceivedAt).ToListAsync();
    }

}
