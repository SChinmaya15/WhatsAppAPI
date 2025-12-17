using backend.Infrastructure;
using backend.Models;
using backend.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/settings")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly MongoRepo _repo;

        public SettingsController(MongoRepo repo)
        {
            _repo = repo;
        }

        [HttpGet("basic")]
        public async Task<IActionResult> Get()
        {
            var settings = await _repo.GetSettingsAsync();
            return Ok(new
            {
                agencyName = settings.AgencyName,
                agencyCode = settings.AgencyCode,
                maxUsers = settings.MaxUsers
            });
        }

        [HttpPut("basic")]
        public async Task<IActionResult> Update([FromBody] Settings updated)
        {
            if (updated.MaxUsers <= 0)
            {
                return BadRequest(new { message = "maxUsers must be greater than zero." });
            }

            var saved = await _repo.UpsertSettingsAsync(updated);
            return Ok(new
            {
                agencyName = saved.AgencyName,
                agencyCode = saved.AgencyCode,
                maxUsers = saved.MaxUsers
            });
        }

        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers()
        {
            var customers = await _repo.GetCustomersAsync();
            return Ok(customers);
        }

        [HttpPost("customers")]
        public async Task<IActionResult> AddCustomer([FromBody] AddCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = HttpContext.Items["TenantId"] as string;

            var customer = new Customer
            {
                Name = dto.Name,
                Email = dto.Email,
                Phone = dto.Phone,
                Source = dto.Source,
                TenantId = tenantId ?? dto.TenantId,
                CustomerId = dto.CustomerId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _repo.CreateCustomerAsync(customer);
            return Ok(customer);
        }

        [HttpPost("customers/import")]
        public async Task<IActionResult> ImportCustomers([FromBody] List<AddCustomerDto> customers)
        {
            if (customers == null || customers.Count == 0)
            {
                return BadRequest(new { message = "No customers provided." });
            }

            var tenantId = HttpContext.Items["TenantId"] as string;

            var valid = customers
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => new Customer
                {
                    Name = c.Name,
                    Email = c.Email,
                    Phone = c.Phone,
                    Source = c.Source,
                    TenantId = tenantId ?? c.TenantId,
                    CustomerId = c.CustomerId,
                    CreatedAt = DateTimeOffset.UtcNow
                })
                .ToList();

            if (valid.Count == 0)
            {
                return BadRequest(new { message = "No valid customers to import." });
            }

            await _repo.CreateCustomersAsync(valid);
            return Ok(new { imported = valid.Count });
        }
    }
}

