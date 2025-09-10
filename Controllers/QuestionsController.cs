using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ScoringApp.DTO.mongo;
using ScoringApp.Services;
using System.Linq;

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

		public record GenerateRequest(string? Title, string? Type, string? Prompt);
		public record UpdateRequest(string? Title, string Content, string? Type, string[]? Options, string[]? CorrectAnswers, string? Answer);
		public record ManualCreateRequest(string? Title, string Content, string? Type, string[]? Options, string[]? CorrectAnswers, string? Answer);
		public record ApproveManyRequest(string[] Ids);

		[HttpPost("generate")]
		public async Task<IActionResult> Generate([FromBody] GenerateRequest req, CancellationToken ct)
		{
			var prompt = string.IsNullOrWhiteSpace(req.Prompt) ? "题库拉取" : req.Prompt;
			var result = await _client.GenerateQuestionAsync(new FastGptClient.QuestionRequest(prompt), ct);
			var rec = await QuestionRepository.CreatePendingIfNotExistsAsync(
				req.Title,
				result.Content,
				req.Type
			);
			return Ok(rec);
		}

		[HttpPost("manual")]
		public async Task<IActionResult> ManualCreate([FromBody] ManualCreateRequest req)
		{
			if (string.IsNullOrWhiteSpace(req.Content)) return BadRequest("content required");
			var existing = await QuestionRepository.FindByHashAsync(QuestionRepository.ComputeNormalizedHash(req.Content));
			if (existing != null) return Conflict(new { message = "题目已存在", id = existing.Id });
			var rec = await QuestionRepository.CreatePendingManualAsync(req.Title, req.Content, req.Type, req.Options, req.CorrectAnswers, req.Answer);
			return Ok(rec);
		}

		[HttpGet]
		public async Task<IActionResult> List([FromQuery] string? status)
		{
			var list = await QuestionRepository.ListAllAsync(status);
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

		[HttpPost("approve-many")]
		public async Task<IActionResult> ApproveMany([FromBody] ApproveManyRequest req)
		{
			if (req?.Ids == null || req.Ids.Length == 0) return BadRequest("ids required");
			var count = await QuestionRepository.ApproveManyAsync(req.Ids.Distinct().ToArray());
			return Ok(new { updated = count });
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