using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ScoringApp.DTO.mongo;

namespace ScoringApp.Controllers
{
	[ApiController]
	[Route("client/auth")]
	public class ClientAuthController : ControllerBase
	{
		public record RegisterRequest(string Username, string Password);
		public record LoginRequest(string Username, string Password);

		[HttpPost("register")]
		public async Task<IActionResult> Register([FromBody] RegisterRequest req)
		{
			if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password)) return BadRequest("username/password required");
			var existing = await ClientUserRepository.FindByUsernameAsync(req.Username);
			if (existing != null) return Conflict("username exists");
			var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
			var user = new ClientUserRecord
			{
				Id = Guid.NewGuid().ToString("N"),
				Username = req.Username,
				PasswordHash = hash,
				CreatedAt = DateTime.UtcNow
			};
			await ClientUserRepository.CreateAsync(user);
			return Ok(new { user.Id, user.Username, user.CreatedAt });
		}

		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginRequest req)
		{
			var user = await ClientUserRepository.FindByUsernameAsync(req.Username);
			if (user == null) return Unauthorized();
			var ok = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash);
			if (!ok) return Unauthorized();
			return Ok(new { user.Id, user.Username });
		}
	}
} 