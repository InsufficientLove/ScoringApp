using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ScoringApp.DTO.mongo;
using ScoringApp.Services;

namespace ScoringApp.Controllers
{
	[ApiController]
	[Route("score")]
	public class ScoreController : ControllerBase
	{
		private readonly ScoreNotifier _notifier;
		private readonly ILogger<ScoreController> _logger;

		public ScoreController(ScoreNotifier notifier, ILogger<ScoreController> logger)
		{
			_notifier = notifier;
			_logger = logger;
		}

		[HttpGet("stream")]
		public async Task Stream([FromQuery] string chatId, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(chatId))
			{
				Response.StatusCode = 400;
				_logger.LogWarning("SSE connect failed: missing chatId, remote={Remote}", HttpContext.Connection.RemoteIpAddress);
				await Response.Body.FlushAsync(ct);
				return;
			}

			Response.Headers.Append("Cache-Control", "no-cache");
			Response.Headers.Append("X-Accel-Buffering", "no");
			Response.ContentType = "text/event-stream";

			var channel = _notifier.Subscribe(chatId);
			HttpContext.Response.OnCompleted(() =>
			{
				_notifier.Unsubscribe(chatId, channel);
				_logger.LogInformation("SSE stream completed: chatId={ChatId}, remote={Remote}", chatId, HttpContext.Connection.RemoteIpAddress);
				return Task.CompletedTask;
			});

			_logger.LogInformation("SSE connected: chatId={ChatId}, remote={Remote}", chatId, HttpContext.Connection.RemoteIpAddress);

			await Response.WriteAsync(": connected\n\n", ct);
			await Response.Body.FlushAsync(ct);

			await foreach (var message in channel.Reader.ReadAllAsync(ct))
			{
				var data = $"event: done\ndata: {message}\n\n";
				await Response.WriteAsync(data, ct);
				await Response.Body.FlushAsync(ct);
				_logger.LogInformation("SSE event sent: chatId={ChatId}, bytes={Bytes}", chatId, data.Length);
			}
		}

		[HttpGet("next")]
		public async Task<IActionResult> Next([FromQuery] string chatId, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(chatId)) return BadRequest("chatId required");
			var rec = await ScoreRepository.FindLatestDoneByChatAsync(chatId, ct);
			if (rec == null)
			{
				_logger.LogInformation("Next: no record for chatId={ChatId}", chatId);
				return NoContent();
			}
			_logger.LogInformation("Next: found record for chatId={ChatId}, id={Id}", chatId, rec.Id);
			return Ok(new
			{
				type = "done",
				id = rec.Id,
				score = rec.Score,
				analysis = rec.Analysis,
				updatedAt = rec.UpdatedAt
			});
		}

		[HttpGet("status")]
		public async Task<IActionResult> Status([FromQuery] string id, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(id)) return BadRequest("id required");
			var rec = await ScoreRepository.FindByIdAsync(id, ct);
			if (rec == null)
			{
				_logger.LogInformation("Status: not found, id={Id}", id);
				return NotFound();
			}
			_logger.LogInformation("Status: found, id={Id}, status={Status}", id, rec.Status);
			return Ok(new
			{
				rec.Status,
				score = rec.Score,
				analysis = rec.Analysis,
				error = rec.Error,
				updatedAt = rec.UpdatedAt
			});
		}
	}
} 