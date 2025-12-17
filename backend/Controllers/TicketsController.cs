using backend.Infrastructure;
using backend.Models;
using backend.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/tickets")]
    [Authorize]
    public class TicketsController : ControllerBase
    {
        private readonly MongoRepo _repo;

        public TicketsController(MongoRepo repo)
        {
            _repo = repo;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? stage = null, [FromQuery] string? search = null)
        {
            var tickets = await _repo.GetTicketsAsync(stage, search);
            var dtos = tickets.Select(t => new TicketDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Name = t.ContactName,
                Email = t.ContactEmail,
                Phone = t.CustomerPhoneNumber,
                Status = t.Status,
                Source = t.Source,
                Priority = t.PriorityScore,
                Budget = t.Budget,
                Created = t.CreatedAt,
                Stage = t.Stage ?? "New",
                Subject = t.Subject,
                Description = t.Description
            }).ToList();

            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var ticket = await _repo.GetTicketByIdAsync(id);
            if (ticket == null) return NotFound();

            var dto = new TicketDto
            {
                Id = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                Name = ticket.ContactName,
                Email = ticket.ContactEmail,
                Phone = ticket.CustomerPhoneNumber,
                Status = ticket.Status,
                Source = ticket.Source,
                Priority = ticket.PriorityScore,
                Budget = ticket.Budget,
                Created = ticket.CreatedAt,
                Stage = ticket.Stage ?? "New",
                Subject = ticket.Subject,
                Description = ticket.Description
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Ticket ticket)
        {
            ticket.CreatedAt = DateTimeOffset.UtcNow;
            ticket.UpdatedAt = DateTimeOffset.UtcNow;
            ticket.TicketNumber = string.IsNullOrWhiteSpace(ticket.TicketNumber)
                ? $"TKT-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}"
                : ticket.TicketNumber;

            await _repo.CreateTicketAsync(ticket);
            return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] Ticket update)
        {
            var ticket = await _repo.GetTicketByIdAsync(id);
            if (ticket == null) return NotFound();

            ticket.ContactName = update.ContactName;
            ticket.ContactEmail = update.ContactEmail;
            ticket.CustomerPhoneNumber = update.CustomerPhoneNumber;
            ticket.Status = update.Status;
            ticket.Source = update.Source;
            ticket.PriorityScore = update.PriorityScore;
            ticket.Budget = update.Budget;
            ticket.Stage = update.Stage;
            ticket.Subject = update.Subject;
            ticket.Description = update.Description;
            ticket.AssignedTo = update.AssignedTo;
            ticket.Metadata = update.Metadata;

            await _repo.UpdateTicketAsync(ticket);
            return NoContent();
        }
    }
}

