namespace backend.Services
{
    public interface IGeminiService
    {
        Task<string?> GetFormalQueryMailBodyAsync(string userText, string userName, CancellationToken ct = default);
    }
}
