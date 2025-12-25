using backend.Models;
using backend.Models.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;

namespace backend.Infrastructure
{
    public class MongoRepo
    {
        private readonly IMongoCollection<MessageRecord> _messages;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<Tenant> _tenants;
        private readonly IMongoCollection<Ticket> _tickets;
        private readonly IMongoCollection<Customer> _customers;
        private readonly IMongoCollection<Settings> _settings;
        
        public MongoRepo(IConfiguration cfg)
        {
            var settings = MongoClientSettings.FromConnectionString(cfg["Mongo:ConnectionString"]);
            // Set the ServerApi field of the settings object to set the version of the Stable API on the client
            settings.ServerApi = new ServerApi(ServerApiVersion.V1);
            var client = new MongoClient(settings);
            var db = client.GetDatabase(cfg["Mongo:Database"]);
            _messages = db.GetCollection<MessageRecord>("messages");
            _users = db.GetCollection<User>("users");
            _tenants = db.GetCollection<Tenant>("tenants");
            _tickets = db.GetCollection<Ticket>("tickets");
            _customers = db.GetCollection<Customer>("customers");
            _settings = db.GetCollection<Settings>("settings");
        }

        // Message methods
        public Task CreateMessageAsync(MessageRecord m) => _messages.InsertOneAsync(m);
        public Task<List<MessageRecord>> GetConversationAsync(string userPhone, string businessNumber) =>
            _messages.Find(m =>
                    (m.From == userPhone && m.To == businessNumber) ||
                    (m.From == businessNumber && m.To == userPhone))
                     .SortBy(m => m.ReceivedAt)
                     .ToListAsync();

        public Task<List<ChatSummaryDto>> GetChatSummariesAsync(string businessNumber)
        {
            var filter = Builders<MessageRecord>.Filter.Or(
                Builders<MessageRecord>.Filter.Eq(m => m.To, businessNumber),
                Builders<MessageRecord>.Filter.Eq(m => m.From, businessNumber));

            var pipeline = _messages.Aggregate()
                .Match(filter)
                .SortByDescending(m => m.ReceivedAt)
                .Project(m => new
                {
                    Participant = m.From == businessNumber ? m.To : m.From,
                    Message = m
                })
                .Group(x => x.Participant, g => new ChatSummaryDto
                {
                    Id = g.Key ?? string.Empty,
                    Phone = g.Key ?? string.Empty,
                    LastMessage = g.First().Message.Body,
                    Timestamp = g.First().Message.ReceivedAt,
                    LastDirection = g.First().Message.Incoming ? "incoming" : "outgoing",
                    UnreadCount = 0
                })
                .SortByDescending(c => c.Timestamp);

            return pipeline.ToListAsync();
        }

        public Task<long> GetMessageCountAsync() =>
            _messages.CountDocumentsAsync(_ => true);

        // User methods
        public Task CreateUserAsync(User user) => _users.InsertOneAsync(user);
        public Task<User?> GetUserByEmailAsync(string email) =>
            _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        public Task<User?> GetUserByIdAsync(string userId) =>
            _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        public Task<List<User>> GetUsersByTenantIdAsync(string tenantId) =>
            _users.Find(u => u.TenantId == tenantId).ToListAsync();

        // Tenant methods
        public Task CreateTenantAsync(Tenant tenant) => _tenants.InsertOneAsync(tenant);
        public Task<Tenant?> GetTenantByIdAsync(string tenantId) =>
            _tenants.Find(t => t.Id == tenantId).FirstOrDefaultAsync();
        public Task<Tenant?> GetTenantByDomainAsync(string domain) =>
            _tenants.Find(t => t.Domain == domain).FirstOrDefaultAsync();
        public Task<List<Tenant>> GetAllTenantsAsync() =>
            _tenants.Find(_ => true).ToListAsync();
        public Task UpdateTenantAsync(Tenant tenant) =>
            _tenants.ReplaceOneAsync(t => t.Id == tenant.Id, tenant);

        // Ticket methods
        public Task CreateTicketAsync(Ticket ticket) => _tickets.InsertOneAsync(ticket);
        public Task<Ticket?> GetTicketByIdAsync(string ticketId) =>
            _tickets.Find(t => t.Id == ticketId).FirstOrDefaultAsync();
        public Task<Ticket?> GetTicketByNumberAsync(string ticketNumber) =>
            _tickets.Find(t => t.TicketNumber == ticketNumber).FirstOrDefaultAsync();
        public Task<List<Ticket>> GetTicketsByCustomerPhoneAsync(string phoneNumber) =>
            _tickets.Find(t => t.CustomerPhoneNumber == phoneNumber)
                    .SortByDescending(t => t.CreatedAt).ToListAsync();
        public Task<List<Ticket>> GetTicketsByStatusAsync(string status) =>
            _tickets.Find(t => t.Status == status)
                    .SortByDescending(t => t.CreatedAt).ToListAsync();
        public Task<List<Ticket>> GetTicketsByTenantIdAsync(string tenantId) =>
            _tickets.Find(t => t.TenantId == tenantId)
                    .SortByDescending(t => t.CreatedAt).ToListAsync();
        public Task<List<Ticket>> GetOpenTicketsByCustomerPhoneAsync(string phoneNumber) =>
            _tickets.Find(t => t.CustomerPhoneNumber == phoneNumber && (t.Status == "Open" || t.Status == "InProgress"))
                    .SortByDescending(t => t.CreatedAt).ToListAsync();
        public Task UpdateTicketAsync(Ticket ticket)
        {
            ticket.UpdatedAt = DateTimeOffset.UtcNow;
            return _tickets.ReplaceOneAsync(t => t.Id == ticket.Id, ticket);
        }
        public Task<long> GetTicketCountAsync() =>
            _tickets.CountDocumentsAsync(_ => true);

        // Customer methods
        public Task CreateCustomerAsync(Customer customer) => _customers.InsertOneAsync(customer);
        public Task CreateCustomersAsync(IEnumerable<Customer> customers) => _customers.InsertManyAsync(customers);
        public Task<List<Customer>> GetCustomersAsync() => _customers.Find(_ => true).SortByDescending(c => c.CreatedAt).ToListAsync();

        public async Task<List<Ticket>> GetTicketsAsync(string? stage = null, string? search = null)
        {
            var filter = Builders<Ticket>.Filter.Empty;

            if (!string.IsNullOrWhiteSpace(stage))
            {
                filter &= Builders<Ticket>.Filter.Eq(t => t.Stage, stage);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var regex = new BsonRegularExpression(search, "i");
                filter &= Builders<Ticket>.Filter.Or(
                    Builders<Ticket>.Filter.Regex(t => t.ContactName, regex),
                    Builders<Ticket>.Filter.Regex(t => t.ContactEmail, regex),
                    Builders<Ticket>.Filter.Regex(t => t.CustomerPhoneNumber, regex)
                );
            }

            return await _tickets.Find(filter)
                .SortByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        // Settings methods (simple singleton doc)
        public async Task<Settings> GetSettingsAsync()
        {
            var existing = await _settings.Find(_ => true).FirstOrDefaultAsync();
            if (existing != null) return existing;

            var defaults = new Settings();
            await _settings.InsertOneAsync(defaults);
            return defaults;
        }

        public async Task<Settings> UpsertSettingsAsync(Settings updated)
        {
            updated.UpdatedAt = DateTimeOffset.UtcNow;
            var existing = await _settings.Find(_ => true).FirstOrDefaultAsync();
            if (existing == null)
            {
                await _settings.InsertOneAsync(updated);
                return updated;
            }

            updated.Id = existing.Id;
            await _settings.ReplaceOneAsync(s => s.Id == existing.Id, updated);
            return updated;
        }

        public Task<long> GetUserCountAsync() =>
            _users.CountDocumentsAsync(_ => true);
    }

}
