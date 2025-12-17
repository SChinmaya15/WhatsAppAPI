using backend.Config;
using backend.Infrastructure;
using backend.Models;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace backend.Services
{
    public partial class WebhookService
    {
        private readonly MongoRepo _repo;
        private readonly WhatsAppOptions _opts;
        private readonly IConversationStore _store;
        private readonly WhatsAppService _whatsAppService;
        private readonly TimeSpan _recentThreshold = TimeSpan.FromHours(24); // treat messages within 24h as conversation continuation
        private static readonly Regex GreetingIdentifierRegex = GreetingRegex();
        private static readonly Regex QueryIdentifierRegex = QueryRegex();
        private static List<ClientDetails> clients = new List<ClientDetails>();

        public WebhookService(MongoRepo repo, IConversationStore store,
            WhatsAppService whatsAppService, IOptions<WhatsAppOptions> opts)
        {
            if (!clients.Any())
            {
                var root = Directory.GetParent(AppContext.BaseDirectory).Parent.Parent.Parent.FullName;
                var filePath = Path.Combine(root, "client details.xlsx");
                clients = ReadClientDetails(filePath);
            }
            _repo = repo;
            _store = store;
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

                            var exists = IdExists(incomingMsgId);
                            if (exists)
                            {
                                return;
                            }
                            else
                            {
                                AddId(incomingMsgId);
                            }
                            // RULE-BASED initial message check
                            bool isInitial = await IsInitialMessageAsync(m, from, text);
                            bool isQuery = IsQueryMessage(text);
                            
                            var rec = new MessageRecord
                            {
                                Body = text,
                                From = from,
                                Incoming = true,
                                To = _opts.BusinessPhoneNumber,
                                ReceivedAt = DateTimeOffset.UtcNow,
                                Status = "received"
                            };

                            // Save message first to get the ID
                            await _repo.CreateMessageAsync(rec);
                            // Track last message time in memory store for conversation recency
                            if (!string.IsNullOrEmpty(from))
                            {
                                await _store.SetLastMessageTimeAsync(from, DateTime.UtcNow);
                            }

                            // Create or update ticket if it's a query
                            Ticket? ticket = null;
                            if (isQuery && !string.IsNullOrEmpty(from))
                            {
                                var customerQuery = text.Split(':');
                                var client=clients.FirstOrDefault(client=>client.CustomerId == customerQuery[0].Trim());
                                ticket = await CreateOrUpdateTicketAsync(rec, from, client?.ClientName +"'s query:"+customerQuery[1], isInitial);
                            }

                            // Generate appropriate reply
                            var replyText = GenerateReplyText(text, isQuery, isInitial, ticket);

                            var resp = await _whatsAppService.SendTextAsync(isInitial,to: from!, text: replyText);
                            var respContent = await resp.Content.ReadAsStringAsync();
                        }
                    }
                }
            }
            catch (Exception ex) {
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
        public class ClientDetails
        {
            public string ClientName { get; set; }
            public string ClientMailId { get; set; }
            public string CustomerId { get; set; }
        }
        Queue<string> lastFiveIds = new Queue<string>();

        private void AddId(string newId)
        {
            // If we already have 5, remove the oldest
            if (lastFiveIds.Count == 5)
                lastFiveIds.Dequeue();

            lastFiveIds.Enqueue(newId);
        }
        bool IdExists(string id)
        {
            return lastFiveIds.Contains(id);
        }
        /// <summary>
        /// Determines if a message is a query (not just a greeting)
        /// </summary>
        private bool IsQueryMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // If it's just a greeting, it's not a query
            if (GreetingIdentifierRegex.IsMatch(text) && text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3)
                return false;

            // Check if message contains question marks or query-related keywords
            if (text.Contains('?') || text.Contains('？'))
                return true;

            // Check for query keywords
            if (QueryIdentifierRegex.IsMatch(text))
                return true;

            // If message is longer than a simple greeting and not just a greeting, treat as query
            if (text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 3)
                return true;

            return false;
        }

        /// <summary>
        /// Determines ticket priority based on query content
        /// </summary>
        private string DeterminePriority(string queryText)
        {
            if (string.IsNullOrWhiteSpace(queryText))
                return "Medium";

            var lowerText = queryText.ToLowerInvariant();

            // Urgent keywords
            if (Regex.IsMatch(lowerText, @"\b(urgent|emergency|critical|asap|immediately|broken|down|not working|error|failed)\b"))
                return "Urgent";

            // High priority keywords
            if (Regex.IsMatch(lowerText, @"\b(important|issue|problem|help|support|complaint)\b"))
                return "High";

            return "Medium";
        }

        /// <summary>
        /// Generates a unique ticket number
        /// </summary>
        private async Task<string> GenerateTicketNumberAsync()
        {
            var ticketCount = await _repo.GetTicketCountAsync();
            var ticketNumber = $"TKT-{DateTimeOffset.UtcNow:yyyyMMdd}-{ticketCount + 1:D6}";
            return ticketNumber;
        }

        /// <summary>
        /// Generates a subject line for the ticket from the query text
        /// </summary>
        private string GenerateTicketSubject(string queryText)
        {
            if (string.IsNullOrWhiteSpace(queryText))
                return "Customer Query";

            // Take first 50 characters or first sentence
            var subject = queryText.Trim();
            var firstSentenceEnd = subject.IndexOfAny(new[] { '.', '!', '?', '？' });
            
            if (firstSentenceEnd > 0 && firstSentenceEnd <= 50)
                subject = subject.Substring(0, firstSentenceEnd);
            else if (subject.Length > 50)
                subject = string.Concat(subject.AsSpan(0, 47), "...");
            
            return subject;
        }

        /// <summary>
        /// Generates appropriate reply text based on message type and ticket status
        /// </summary>
        private string GenerateReplyText(string incomingText, bool isQuery, bool isInitial, Ticket? ticket)
        {
            if (!isQuery)
            {
                // Greeting or simple message
                return $"Hi! Please send your query in this format  <CustId> : <Query>";
            }

            if (ticket != null)
            {
                if (isInitial)
                {
                    return $"Thank you for your query. We have created a support ticket #{ticket.TicketNumber} for your request. Our team will get back to you soon.";
                }
                else
                {
                    return $"Thank you for the additional information. We've updated your ticket #{ticket.TicketNumber}. Our team is working on it.";
                }
            }

            // Fallback
            return $"Thank you for your message. We have received your query and will respond shortly.";
        }
       

        private async Task<bool> IsInitialMessageAsync(JsonElement msgElement, string? from, string? incomingText)
        {
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

        /// <summary>
        /// Creates a new ticket or updates existing open ticket for the customer
        /// </summary>
        private async Task<Ticket?> CreateOrUpdateTicketAsync(MessageRecord message, string customerPhone, string queryText, bool isInitial)
        {
            try
            {
                // Check if there's an existing open ticket for this customer
                var openTickets = await _repo.GetOpenTicketsByCustomerPhoneAsync(customerPhone);
                Ticket ticket;

                if (openTickets.Any() && !isInitial)
                {
                    // Update existing open ticket
                    ticket = openTickets.First();
                    ticket.Description += $"\n\n[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}] {queryText}";
                    ticket.MessageIds.Add(message.Id!);
                    ticket.UpdatedAt = DateTimeOffset.UtcNow;
                    await _repo.UpdateTicketAsync(ticket);
                }
                else
                {
                    // Create new ticket
                    var ticketNumber = await GenerateTicketNumberAsync();

                    // Try to find user by phone (if registered)
                    // This is optional - you may need to store phone numbers in User model
                    string? customerId = null;
                    string? tenantId = null;

                    ticket = new Ticket
                    {
                        TicketNumber = ticketNumber,
                        Subject = GenerateTicketSubject(queryText),
                        Description = queryText,
                        Status = "Open",
                        Priority = DeterminePriority(queryText),
                        CustomerPhoneNumber = customerPhone,
                        CustomerId = customerId,
                        TenantId = tenantId,
                        InitialMessageId = message.Id,
                        MessageIds = new List<string> { message.Id! },
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    await _repo.CreateTicketAsync(ticket);
                }

                return ticket;
            }
            catch (Exception ex)
            {
                // Log error but don't fail the message processing
                Console.WriteLine($"Error creating/updating ticket: {ex.Message}");
                return null;
            }
        }

        [GeneratedRegex(@"\b(hi|hello|hey|get started|start|good morning|good afternoon)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-IN")]
        private static partial Regex GreetingRegex();

        [GeneratedRegex(@"\b(question|query|help|support|issue|problem|complaint|request|need|want|how|what|when|where|why|can|could|would|please|help me|i need|i want)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-IN")]
        private static partial Regex QueryRegex();
    }
}
