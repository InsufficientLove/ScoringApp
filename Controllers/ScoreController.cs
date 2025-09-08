using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ScoringApp.DTO.mongo;
using ScoringApp.Services;

namespace ScoringApp.Controllers
{
	[ApiController]
	[Route("score")]
	public class ScoreController : ControllerBase
	{
		private readonly ScoreNotifier _notifier;

		public ScoreController(ScoreNotifier notifier)
		{
			_notifier = notifier;
		}

		[HttpGet("stream")]
		public async Task Stream([FromQuery] string userId, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(userId))
			{
				Response.StatusCode = 400;
				await Response.Body.FlushAsync(ct);
				return;
			}

			Response.Headers.Add("Cache-Control", "no-cache");
			Response.Headers.Add("X-Accel-Buffering", "no");
			Response.ContentType = "text/event-stream";

			var channel = _notifier.Subscribe(userId);
			HttpContext.Response.OnCompleted(() =>
			{
				_notifier.Unsubscribe(userId, channel);
				return Task.CompletedTask;
			});

			await Response.WriteAsync(": connected\n\n", ct);
			await Response.Body.FlushAsync(ct);

			await foreach (var message in channel.Reader.ReadAllAsync(ct))
			{
				var data = $"event: done\ndata: {message}\n\n";
				await Response.WriteAsync(data, ct);
				await Response.Body.FlushAsync(ct);
			}
		}

		[HttpGet("next")]
		public async Task<IActionResult> Next([FromQuery] string userId, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId required");
			var rec = await ScoreRepository.FindLatestDoneByUserAsync(userId, ct);
			if (rec == null) return NoContent();
			return Ok(new
			{
				type = "done",
				rec.ClientAnswerId,
				score = rec.Score,
				feedback = rec.Feedback,
				updatedAt = rec.UpdatedAt
			});
		}

		[HttpGet("status")]
		public async Task<IActionResult> Status([FromQuery] string clientAnswerId, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(clientAnswerId)) return BadRequest("clientAnswerId required");
			var rec = await ScoreRepository.FindByClientAnswerIdAsync(clientAnswerId, ct);
			if (rec == null) return NotFound();
			return Ok(new
			{
				rec.Status,
				score = rec.Score,
				feedback = rec.Feedback,
				error = rec.Error,
				updatedAt = rec.UpdatedAt
			});
		}
	}
} 