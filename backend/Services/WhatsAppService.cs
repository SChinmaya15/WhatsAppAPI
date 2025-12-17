using System.Text;
using backend.Config;
using backend.Models;
using backend.Infrastructure;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using static System.Net.Mime.MediaTypeNames;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using System.Text.Json;

namespace backend.Services
{
    public class WhatsAppService
    {
        private readonly MongoRepo _repo;
        private readonly WhatsAppOptions _opts;
        private readonly IHttpClientFactory _http;
        private readonly IEmailService _emailService;

        public WhatsAppService(IHttpClientFactory http, MongoRepo repo
            , IEmailService emailService, IOptions<WhatsAppOptions> opts)
        {
            _http = http;
            _repo = repo;
            _opts = opts.Value;
            _emailService = emailService;
        }

        public async Task<HttpResponseMessage> SendTextAsync(bool isInitial,string to, string text, bool useTemplate = false, object templatePayload = null)
        {
            var client = _http.CreateClient("meta");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opts.AccessToken);

            var body = new WhatsAppRequest
            {
                to = to,
                messaging_product = "whatsapp",
                type = useTemplate ? "template" : "text",
                text = useTemplate ? null : new { body = text },
                template = useTemplate ? templatePayload : null,
            };

            var replyText = PopulateMessageContent(to, text);

            // Use graph API with the phone number id path (versioned path optional)
            var reqUri = $"{_opts.PhoneNumberId}/messages";
            StringContent? content = null;
            //if (isInitial) {
            //    var json = await SendAskForIdCardAsync(to);
            //    content = new StringContent(json, Encoding.UTF8, "application/json");
            //}
            //else
            //{
                content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            //}
            var resp = await client.PostAsync(reqUri, content);
            if (resp.StatusCode == System.Net.HttpStatusCode.OK)
            { 
                await _repo.CreateMessageAsync(replyText);
                try
                {
                    var message = $"Message to {to} was sent successfully at {DateTime.UtcNow}.";
                    var emailBodyText = PopulateMessageContent(to, message);
                    await _emailService.SendEmailAsync(
                        subject: "WhatsApp Message Sent",
                        toEmail: "imbhavneetkumar@gmail.com",
                        body: $"Message to {to} was sent successfully at {DateTime.UtcNow}."
                    );
                    await _repo.CreateMessageAsync(emailBodyText);
                }
                catch (Exception ex)
                {
                    // Log but do not interrupt WhatsApp flow
                    Console.WriteLine("Email sending failed: " + ex.Message);
                }
            }
            return resp;
        }
        public async Task<string> SendAskForIdCardAsync(string toPhoneNumber)
        {
            var payload = new
            {
                messaging_product = "whatsapp",
                to = toPhoneNumber,
                type = "interactive",
                interactive = new
                {
                    type = "list",
                    header = new
                    {
                        type = "text",
                        text = "Verification Required"
                    },
                    body = new
                    {
                        text = "Please provide your Customer ID to continue."
                    },
                    footer = new
                    {
                        text = "Tap below to proceed"
                    },
                    action = new
                    {
                        button = "Provide ID",
                        sections = new[]
                        {
                    new
                    {
                        title = "Verification",
                        rows = new[]
                        {
                            new
                            {
                                id = "ENTER_CUSTOMER_ID",
                                title = "Enter Customer ID",
                                description = "Provide your 6-digit ID"
                            }
                        }
                    }
                }
                    }
                }
            };

            //using var client = new HttpClient();
            //client.DefaultRequestHeaders.Authorization =
            //    new AuthenticationHeaderValue("Bearer", accessToken);

            var json = JsonSerializer.Serialize(payload);
            return json;
            //var content = new StringContent(json, Encoding.UTF8, "application/json");

            //await client.PostAsync(url, content);
        }
        private MessageRecord PopulateMessageContent(string to, string text)
        {
            return new MessageRecord
            {
                To = to,
                Body = text,
                Incoming = false,
                From = _opts.BusinessPhoneNumber,
                ReceivedAt = DateTimeOffset.UtcNow
            };
        }
    }

}
