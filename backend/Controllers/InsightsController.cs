using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/insights")]
    [Authorize]
    public class InsightsController : ControllerBase
    {
        private readonly MongoRepo _repo;

        public InsightsController(MongoRepo repo)
        {
            _repo = repo;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            var messages = await _repo.GetMessageCountAsync();
            var users = await _repo.GetUserCountAsync();
            var tickets = await _repo.GetTicketCountAsync();
            var openTickets = await _repo.GetTicketsByStatusAsync("Open");

            return Ok(new
            {
                totalMessages = messages,
                totalUsers = users,
                totalTickets = tickets,
                openTickets = openTickets.Count
            });
        }
    }
}

