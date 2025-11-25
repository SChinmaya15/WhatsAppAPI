using Microsoft.Extensions.Caching.Memory;

namespace backend.Services
{
    public class MemoryConversationStore : IConversationStore
    {
        private readonly TimeSpan _ttl;
        private readonly IMemoryCache _cache;
        
        public MemoryConversationStore(IMemoryCache cache, TimeSpan ttl)
        {
            _ttl = ttl;
            _cache = cache;
        }

        public Task<DateTime?> GetLastMessageTimeAsync(string sender)
        {
            if (_cache.TryGetValue(sender, out DateTime last))
                return Task.FromResult<DateTime?>(last);

            return Task.FromResult<DateTime?>(null);
        }

        public Task SetLastMessageTimeAsync(string sender, DateTime timestamp)
        {
            _cache.Set(sender, timestamp, _ttl);
            return Task.CompletedTask;
        }
    }
}
