using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ScoringApp.DTO.mongo;
using ScoringApp.Services;

namespace ScoringApp.Controllers
{
	[ApiController]
	[Route("questions")]
	public class QuestionsController : ControllerBase
	{
		private readonly FastGptClient _client;
		public QuestionsController(FastGptClient client)
		{
			_client = client;
		}

		public record GenerateRequest(string UserId, string Prompt, string? Title, string? Type);
		public record UpdateRequest(string? Title, string Content, string? Type, string[]? Options, string[]? CorrectAnswers, string? Answer);

		[HttpPost("generate")]
		public async Task<IActionResult> Generate([FromBody] GenerateRequest req, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(req.UserId) || string.IsNullOrWhiteSpace(req.Prompt)) return BadRequest("UserId/Prompt required");
			var result = await _client.GenerateQuestionAsync(new FastGptClient.QuestionRequest(req.Prompt), ct);
			var rec = await QuestionRepository.CreatePendingIfNotExistsAsync(
				req.UserId,
				req.Title,
				result.Content,
				req.Type
			);
			return Ok(rec);
		}

		[HttpGet]
		public async Task<IActionResult> List([FromQuery] string userId)
		{
			if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId required");
			var list = await QuestionRepository.ListByUserAsync(userId);
			return Ok(list);
		}

		[HttpPut("{id}")]
		public async Task<IActionResult> Update(string id, [FromBody] UpdateRequest req)
		{
			var found = await QuestionRepository.FindByIdAsync(id);
			if (found == null) return NotFound();
			await QuestionRepository.UpdateAsync(id, req.Title, req.Content, req.Type, req.Options, req.CorrectAnswers, req.Answer);
			return Ok();
		}

		[HttpPost("{id}/approve")]
		public async Task<IActionResult> Approve(string id)
		{
			var found = await QuestionRepository.FindByIdAsync(id);
			if (found == null) return NotFound();
			await QuestionRepository.ApproveAsync(id);
			return Ok();
		}

		[HttpDelete("{id}")]
		public async Task<IActionResult> Delete(string id)
		{
			var found = await QuestionRepository.FindByIdAsync(id);
			if (found == null) return NotFound();
			await QuestionRepository.DeleteAsync(id);
			return Ok();
		}
	}
} 