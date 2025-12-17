using backend.Config;
using backend.Infrastructure;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/chat")]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly MongoRepo _repo;
        private readonly WhatsAppOptions _whatsAppOptions;
        private readonly WhatsAppService _whatsAppService;
        
        public MessagesController(MongoRepo repo, 
            WhatsAppService whatsAppService, IOptions<WhatsAppOptions> options)
        {
            _repo = repo;
            _whatsAppOptions = options.Value;
            _whatsAppService = whatsAppService;
        }

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SendMessage message)
        {
            try
            {
                var record = new MessageRecord
                {
                    To = message.To,
                    Incoming = false,
                    Status = "queued",
                    Body = message.Message,
                    From = _whatsAppOptions.BusinessPhoneNumber,
                };
                // Save intent to DB (optimistic)
                await _repo.CreateMessageAsync(record);

                var resp = await _whatsAppService.SendTextAsync(false,message.To, message.Message);
                if (!resp.IsSuccessStatusCode)
                {
                    // Update DB etc - simplified
                    return StatusCode((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
                }

                var json = await resp.Content.ReadAsStringAsync();

                // parse message id from meta response to update DB (omitted)
                return Ok(new { status = "sent", meta = json });
            }
            catch(Exception ex)
            {
                return new BadRequestObjectResult(new { status = "failed", meta = ex.Message });
            }
        }

        [HttpGet("history/{userPhone}")]
        public async Task<IActionResult> History(string userPhone, [FromQuery] string businessNumber = null)
        {
            var businessNum = businessNumber ?? _whatsAppOptions.BusinessPhoneNumber;
            var list = await _repo.GetConversationAsync(userPhone, businessNum);
            return Ok(list);
        }
    }
}
