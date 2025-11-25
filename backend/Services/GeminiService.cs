using System.Text;
using System.Text.Json;

namespace backend.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IConfiguration _config;
        private readonly string _apiKey;
        private readonly string _model; // e.g. "gemini-2.5" or full Vertex resource path
        private readonly string _endpoint; // e.g. "https://generativelanguage.googleapis.com/v1beta/models/"

        public GeminiService(IHttpClientFactory clientFactory, IConfiguration config)
        {
            _clientFactory = clientFactory;
            _config = config;
            _apiKey = config["Gemini:ApiKey"] ?? throw new ArgumentNullException("Gemini:ApiKey");
            _model = config["Gemini:Model"] ?? "gemini-2.5-flash";
            _endpoint = config["Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/models";
        }

        /// <summary>
        /// Calls Gemini to create a formal query email (JSON with subject and body) using userText and userName.
        /// Returns the email body string (or null on failure).
        /// </summary>
        public async Task<string?> GetFormalQueryMailBodyAsync(string userText, string userName, CancellationToken ct = default)
        {
            var client = _clientFactory.CreateClient();

            // Build URL: model endpoint + :generateContent (or correct endpoint for your setup)
            var url = $"{_endpoint}/{_model}:generateContent?key={_apiKey}";

            // Prompt: ask model to return only a single-line JSON object with subject and body
            var prompt = $@"
You are a professional assistant that converts a user's informal query into a formal query email.
Return ONLY a single-line JSON object with two keys:
  - ""body"": full formal email body. Include a formal greeting (use recipient placeholder 'Dear Sir/Madam,' if no recipient given), a concise description of the query, any clarifying questions, a call to action, and a closing with the sender name.

Do NOT include any commentary, explanation, or extra fields.

User-supplied details:
Sender name: ""{EscapeForPrompt(userName)}""
Query details: ""{EscapeForPrompt(userText)}""
";

            var payload = new
            {
                contents = new[]
                {
                new {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            // If your endpoint requires header instead of ?key=, add it here:
            // client.DefaultRequestHeaders.Remove("x-goog-api-key");
            // client.DefaultRequestHeaders.Add("x-goog-api-key", _apiKey);

            var resp = await client.PostAsync(url, content, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                throw new Exception($"Gemini API error: {resp.StatusCode} {err}");
            }

            var respStr = await resp.Content.ReadAsStringAsync(ct);

            // Attempt to extract first JSON object from the response text and parse it
            var extractedJson = ExtractFirstJsonObject(respStr);
            if (extractedJson != null)
            {
                try
                {
                    return GetMailBody(extractedJson);
                    //using var doc = JsonDocument.Parse(extractedJson);
                    //if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    //{
                    //    if (doc.RootElement.TryGetProperty("body", out var bodyEl))
                    //    {
                    //        return bodyEl.GetString();
                    //    }

                    //    // if there is subject + body, but different casing or nesting, try to handle common cases
                    //    if (doc.RootElement.TryGetProperty("Body", out var bodyEl2))
                    //        return bodyEl2.GetString();
                    //}
                }
                catch
                {
                    // ignore parsing error and fallback to raw text
                }
            }

            // Fallback: if no JSON found/parsable, return the entire response (model output)
            return respStr;
        }

        private static string? ExtractFirstJsonObject(string s)
        {
            int start = s.IndexOf('{');
            if (start < 0) return null;
            int depth = 0;
            for (int i = start; i < s.Length; i++)
            {
                if (s[i] == '{') depth++;
                if (s[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return s.Substring(start, i - start + 1);
                    }
                }
            }
            return null;
        }
        private string GetMailBody(string jsonString)
        {

            using var doc = JsonDocument.Parse(jsonString);

            // navigate to candidates[0].content.parts[0].text
            string rawText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            // rawText contains the JSON inside ```json ... ```, so we clean it
            string cleaned = rawText
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            // parse the inner JSON
            using var innerDoc = JsonDocument.Parse(cleaned);

            // final mail body
            string mailBody = innerDoc.RootElement.GetProperty("body").GetString();

            return mailBody;
        }
        private static string EscapeForPrompt(string input)
        {
            if (input is null) return string.Empty;
            return input.Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
