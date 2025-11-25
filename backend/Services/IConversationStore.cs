namespace backend.Services
{
    public interface IConversationStore
    {
        Task<DateTime?> GetLastMessageTimeAsync(string sender);
        Task SetLastMessageTimeAsync(string sender, DateTime timestamp);
    }
}