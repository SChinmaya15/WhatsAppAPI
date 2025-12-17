using backend.Infrastructure;
using backend.Models;
using backend.Models.DTOs;
using backend.Config;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using BCrypt.Net;
using System.ComponentModel.DataAnnotations;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [Authorize]
    public class AuthController : ControllerBase
    {
        private readonly MongoRepo _repo;
        private readonly JwtTokenService _jwt;
        private readonly JwtOptions _jwtOptions;

        public AuthController(MongoRepo repo, JwtTokenService jwtTokenService, IOptions<JwtOptions> jwtOptions)
        {
            _repo = repo;
            _jwt = jwtTokenService;
            _jwtOptions = jwtOptions.Value;
        }

        /// <summary>
        /// Register a new user
        /// </summary>
        [HttpPost("register/user")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterUserDto dto)
        {
            try
            {
                // Validate input
                if (!ModelState.IsValid)
                {
                    return BadRequest(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid input data"
                    });
                }

                // Check if user already exists
                var existingUser = await _repo.GetUserByEmailAsync(dto.Email);
                if (existingUser != null)
                {
                    return Conflict(new AuthResponseDto
                    {
                        Success = false,
                        Message = "User with this email already exists"
                    });
                }

                // If TenantId is provided, verify tenant exists
                if (!string.IsNullOrEmpty(dto.TenantId))
                {
                    var tenant = await _repo.GetTenantByIdAsync(dto.TenantId);
                    if (tenant == null)
                    {
                        return BadRequest(new AuthResponseDto
                        {
                            Success = false,
                            Message = "Specified tenant does not exist"
                        });
                    }
                }

                // Hash password
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

                // Create user
                var user = new User
                {
                    Email = dto.Email,
                    PasswordHash = passwordHash,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    TenantId = dto.TenantId,
                    Role = "User",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    IsActive = true
                };

                await _repo.CreateUserAsync(user);

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    UserId = user.Id,
                    TenantId = user.TenantId,
                    Email = user.Email,
                    Message = "User registered successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new AuthResponseDto
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Register a new tenant with owner user
        /// </summary>
        [HttpPost("register/tenant")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterTenant([FromBody] RegisterTenantDto dto)
        {
            try
            {
                // Validate input
                if (!ModelState.IsValid)
                {
                    return BadRequest(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid input data"
                    });
                }

                // Check if tenant domain already exists (if provided)
                if (!string.IsNullOrEmpty(dto.Domain))
                {
                    var existingTenant = await _repo.GetTenantByDomainAsync(dto.Domain);
                    if (existingTenant != null)
                    {
                        return Conflict(new AuthResponseDto
                        {
                            Success = false,
                            Message = "Tenant with this domain already exists"
                        });
                    }
                }

                // Check if owner email already exists
                var existingUser = await _repo.GetUserByEmailAsync(dto.OwnerEmail);
                if (existingUser != null)
                {
                    return Conflict(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Owner email already exists"
                    });
                }

                // Create tenant
                var tenant = new Tenant
                {
                    Name = dto.Name,
                    Domain = dto.Domain,
                    Description = dto.Description,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    IsActive = true,
                    Settings = new Dictionary<string, object>()
                };

                await _repo.CreateTenantAsync(tenant);

                // Create owner user
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.OwnerPassword);
                var owner = new User
                {
                    Email = dto.OwnerEmail,
                    PasswordHash = passwordHash,
                    FirstName = dto.OwnerFirstName,
                    LastName = dto.OwnerLastName,
                    TenantId = tenant.Id,
                    Role = "Admin", // Tenant owner is an admin
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    IsActive = true
                };

                await _repo.CreateUserAsync(owner);

                // Update tenant with owner ID
                tenant.OwnerId = owner.Id;
                await _repo.UpdateTenantAsync(tenant);

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    UserId = owner.Id,
                    TenantId = tenant.Id,
                    Email = owner.Email,
                    Message = "Tenant and owner registered successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new AuthResponseDto
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Log in and receive a JWT
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid input data"
                });
            }

            var user = await _repo.GetUserByEmailAsync(dto.Email);
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                return Unauthorized(new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid email or password"
                });
            }

            var passwordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!passwordValid)
            {
                return Unauthorized(new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid email or password"
                });
            }

            if (!user.IsActive)
            {
                return Forbid();
            }

            var token = _jwt.GenerateToken(user);
            return Ok(new AuthResponseDto
            {
                Success = true,
                UserId = user.Id,
                TenantId = user.TenantId,
                Email = user.Email,
                Token = token,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
                Message = "Login successful"
            });
        }
    }
}

