using backend.Config;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/chats")]
    [Authorize]
    public class ChatsController : ControllerBase
    {
        private readonly MongoRepo _repo;
        private readonly WhatsAppOptions _options;

        public ChatsController(MongoRepo repo, IOptions<WhatsAppOptions> options)
        {
            _repo = repo;
            _options = options.Value;
        }

        [HttpGet]
        public async Task<IActionResult> GetChats([FromQuery] string? businessNumber = null)
        {
            var business = string.IsNullOrWhiteSpace(businessNumber)
                ? _options.BusinessPhoneNumber
                : businessNumber;

            if (string.IsNullOrWhiteSpace(business))
            {
                return BadRequest(new { message = "Business phone number is not configured." });
            }

            var chats = await _repo.GetChatSummariesAsync(business);
            return Ok(chats);
        }
    }
}

