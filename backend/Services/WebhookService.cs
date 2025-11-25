using backend.Config;
using backend.Infrastructure;
using backend.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using ClosedXML.Excel;

namespace backend.Services
{
    public partial class WebhookService
    {
        private readonly MongoRepo _repo;
        private readonly WhatsAppOptions _opts;
        private readonly IConversationStore _store;
        private readonly WhatsAppService _whatsAppService;
        private readonly GeminiService _geminiService;
        private readonly TimeSpan _recentThreshold = TimeSpan.FromHours(24); // treat messages within 24h as conversation continuation
        private static readonly Regex GreetingIdentifierRegex = GreetingRegex();
        private static List<ClientDetails> clients=new List<ClientDetails>();

        public WebhookService(MongoRepo repo,
            WhatsAppService whatsAppService, IOptions<WhatsAppOptions> opts, IConversationStore store,
            GeminiService geminiService)
        {
            if(!clients.Any())
            clients = ReadClientDetails(@"client details.xlsx");

            _geminiService = geminiService;
            _store = store;
            _repo = repo;
            _opts = opts.Value;
            _whatsAppService = whatsAppService;
        }

        public async Task SaveMessage(JsonElement element)
        {
            try
            {
                if (element.ValueKind != JsonValueKind.Undefined)
                {
                    foreach (var m in element.EnumerateArray())
                    {
                        string from = m.GetProperty("from").GetString() ?? "";
                        string type = m.GetProperty("type").GetString() ?? "text";

                        if (type == "text")
                        {
                            string incomingMsgId = m.GetProperty("id").GetString() ?? "";
                            string text = m.GetProperty("text").GetProperty("body").GetString() ?? "";

                            // RULE-BASED initial message check
                            bool isInitial  = Regex.IsMatch(
                                                 text,
                                                 @"\b(hi|hello|hey|get started|start|good morning|good afternoon)\b",
                                                 RegexOptions.IgnoreCase);

                            var rec = new MessageRecord
                            {
                                Body = text,
                                From = from,
                                Incoming = true,
                                To = _opts.BusinessPhoneNumber,
                                ReceivedAt = DateTimeOffset.UtcNow
                            };
                            string replyText = string.Empty;
                            string mailBody = string.Empty;
                            if (isInitial)
                            {
                                replyText = $"Hi, Please mention Your Query in this format \"<CustID> :<Query>\"";//";
                            }
                            else
                            {
                                var pattern = @"^(?<CustId>\d{1,6})\s*:\s*(?<message>.+)$";
                                var match = Regex.Match(text, pattern);
                                if (match.Success)
                                {
                                    var ticket = match.Groups["CustId"].Value;
                                    if (!clients.Where(cust => cust.CustomerId == ticket).Any())
                                    {
                                        replyText = $"Sorry there is no one with the CustomerId: {ticket} with us.";
                                    }
                                    else
                                    {
                                        var message = match.Groups["message"].Value;
                                        mailBody = await _geminiService.GetFormalQueryMailBodyAsync(message, getUserName(ticket), CancellationToken.None);
                                        replyText = "Query has been registered successfully.";
                                    }
                                }
                                
                            }
                            var resp = await _whatsAppService.SendTextAsync(to: from!, text: replyText, mailBody);
                            var respContent = await resp.Content.ReadAsStringAsync();

                            await _repo.CreateMessageAsync(rec);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }
        public static List<ClientDetails> ReadClientDetails(string filePath)
        {
            var list = new List<ClientDetails>();

            var workbook = new XLWorkbook(filePath);
            var ws = workbook.Worksheet(1); // first worksheet

            // Detect header row (assumes headers are in row 1)
            var headerRow = 1;
            var lastRow = ws.LastRowUsed().RowNumber();
            var lastCol = ws.LastColumnUsed().ColumnNumber();

            // Optional: find column indexes by header name for safety
            var colClientName = 1;   // default fallbacks
            var colClientMailId = 2;
            var colCustomerId = 3;

            // Try to detect by header text (case-insensitive)
            for (int col = 1; col <= lastCol; col++)
            {
                var header = ws.Cell(headerRow, col).GetString().Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(header)) continue;
                if (header.Contains("client name")) colClientName = col;
                else if (header.Contains("client mail")) colClientMailId = col;
                else if (header.Contains("customer id") || header.Contains("customerid")) colCustomerId = col;
            }

            // Read rows (start at headerRow + 1)
            for (int row = headerRow + 1; row <= lastRow; row++)
            {
                var name = ws.Cell(row, colClientName).GetString().Trim();
                var mail = ws.Cell(row, colClientMailId).GetString().Trim();
                var cid = ws.Cell(row, colCustomerId).GetString().Trim();

                // skip blank rows
                if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(mail) && string.IsNullOrEmpty(cid))
                    continue;

                list.Add(new ClientDetails
                {
                    ClientName = name,
                    ClientMailId = mail,
                    CustomerId = cid
                });
            }

            return list;
        }


        private async Task<bool> IsInitialMessageAsync(JsonElement msgElement, string? from, string? incomingText)
        {
            return false;
            // 1) If the incoming payload explicitly has context.message_id, it's a reply (NOT initial)
            if (msgElement.TryGetProperty("context", out var ctx) && ctx.ValueKind == JsonValueKind.Object)
            {
                if (ctx.TryGetProperty("message_id", out var mid) && !string.IsNullOrEmpty(mid.GetString()))
                {
                    //_logger.LogDebug("context.message_id present -> not initial");
                    return false;
                }
            }

            // 2) Check server-side history: if we have a recent message from this user within threshold, treat as continuation (NOT initial)
            if (!string.IsNullOrEmpty(from))
            {
                var last = await _store.GetLastMessageTimeAsync(from);
                if (last.HasValue && (DateTime.UtcNow - last.Value) <= _recentThreshold)
                {
                    //_logger.LogDebug("Found recent history (within threshold) -> not initial");
                    return false;
                }
            }

            // 3) Rule: common greetings often signal user starting conversation -> treat as initial
            if (!string.IsNullOrEmpty(incomingText) && GreetingIdentifierRegex.IsMatch(incomingText))
            {
                //_logger.LogDebug("Greeting matched -> initial");
                return true;
            }

            // 4) Default: if no evidence of prior conversation, treat as initial
            // (This is conservative  you can invert this rule to default to NOT initial)
            //_logger.LogDebug("No recent history and no context -> initial by default");
            return true;
        }

        [GeneratedRegex(@"\b(hi|hello|hey|get started|start|good morning|good afternoon)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-IN")]
        private static partial Regex GreetingRegex();

        private string getUserName(string id)
        {
            // Dummy list
            var items = new List<User>
                        {
                            new User { Id = "1", Name = "Alpha" },
                            new User { Id = "2", Name = "Beta" },
                            new User { Id = "3", Name = "Gamma" },
                            new User { Id = "4", Name = "Delta" },
                            new User { Id = "5", Name = "Omega" }
                        };
            return items.Where(usr => usr.Id == id).FirstOrDefault().Name;
        }
    }

    public class ClientDetails
    {
        public string ClientName { get; set; }
        public string ClientMailId { get; set; }
        public string CustomerId { get; set; }
    }
}
