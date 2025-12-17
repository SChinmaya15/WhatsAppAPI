using backend.Config;
using System.Text.Json;
using backend.Services;
using backend.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace backend.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class WebhookController : ControllerBase
    {
        public readonly MongoRepo _repo;
        private readonly WebhookService _service;
        private readonly WhatsAppOptions _whatsAppOptions;
        
        public WebhookController(MongoRepo repo, WebhookService service, IOptions<WhatsAppOptions> options)
        {
            _repo = repo;
            _service = service;
            _whatsAppOptions = options.Value;
        }

        [HttpGet]
        public IActionResult Verify([FromQuery(Name = "hub.mode")] string mode,
                                    [FromQuery(Name = "hub.verify_token")] string token,
                                    [FromQuery(Name = "hub.challenge")] string challenge)
        {
            if (mode == "subscribe" && token == _whatsAppOptions.VerifyToken) return Ok(challenge);
            return Forbid();
        }

        [HttpPost]
        public async Task<IActionResult> Receive()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            Console.WriteLine("Incoming: " + body);

            if (string.IsNullOrWhiteSpace(body))
            {
                // No JSON payload sent, just return 200 OK
                return Ok("Empty payload");
            }

            JsonDocument payload;
            try
            {
                payload = JsonDocument.Parse(body);
            }
            catch (JsonException ex)
            {
                // Invalid JSON sent
                Console.WriteLine("Invalid JSON: " + ex.Message);
                return BadRequest("Invalid JSON payload");
            }

            if (!payload.RootElement.TryGetProperty("entry", out var entryArray) ||
                entryArray.GetArrayLength() == 0)
            {
                return Ok("No entry data");
            }

            var changesArray = entryArray[0].GetProperty("changes");
            if (changesArray.GetArrayLength() == 0)
            {
                return Ok("No changes data");
            }

            var value = changesArray[0].GetProperty("value");
            
            // --------- HANDLE INCOMING MESSAGES ----------
            if (value.TryGetProperty("messages", out var messages))
            {
                await _service.SaveMessage(messages);
            }

            // --------- HANDLE STATUS UPDATES ----------
            if (value.TryGetProperty("statuses", out var statuses))
            {
                foreach (var status in statuses.EnumerateArray())
                {
                    var msgId = status.GetProperty("id").GetString();
                    var stat = status.GetProperty("status").GetString();
                    Console.WriteLine($"Status update: {msgId} => {stat}");
                }

                return Ok();
            }

            return Ok("No handled event type");
        }
    }
}
