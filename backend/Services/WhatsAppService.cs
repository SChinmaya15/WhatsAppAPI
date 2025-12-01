using backend.Config;
using backend.Infrastructure;
using backend.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using JsonConvert = Newtonsoft.Json.JsonConvert;

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

        public async Task<HttpResponseMessage> SendTextAsync(string to, string text, string mailBody, string custId,string toMail, bool useTemplate = false, object templatePayload = null)
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
            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            
            var resp = await client.PostAsync(reqUri, content);
            if (resp.StatusCode == System.Net.HttpStatusCode.OK)
            { 
               // await _repo.CreateMessageAsync(replyText);
                try
                {
                    var message = $"Message to {to} was sent successfully at {DateTime.UtcNow}.";
                    var emailBodyText = PopulateMessageContent(to, message);
                    if(mailBody!="")
                    await _emailService.SendEmailAsync(
                        subject: $"Query Raised from Customer: {custId}",
                        toEmail: string.IsNullOrEmpty(toMail) ?"samchinmaya15@gmail.com":toMail,
                        body: mailBody
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
