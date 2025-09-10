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
			try
			{
				var result = await _client.GenerateQuestionAsync(new FastGptClient.QuestionRequest(prompt), ct);
				var created = 0;
				try
				{
					using var doc = System.Text.Json.JsonDocument.Parse(result.Content);
					var root = doc.RootElement;
					if (root.TryGetProperty("select", out var selects) && selects.ValueKind == System.Text.Json.JsonValueKind.Array)
					{
						foreach (var item in selects.EnumerateArray())
						{
							var content = item.GetProperty("content").GetString();
							var opts = item.GetProperty("options");
							var correct = item.GetProperty("correct_answer").GetString();
							var options = opts.EnumerateObject().Select(p => $"{p.Name}:{p.Value.GetString()}").ToArray();
							var rec = await QuestionRepository.CreatePendingFastGptIfNotExistsAsync(null, content!, "choice", options, new[] { correct! }, null);
							created++;
						}
					}
					if (root.TryGetProperty("fill", out var fills) && fills.ValueKind == System.Text.Json.JsonValueKind.Array)
					{
						foreach (var item in fills.EnumerateArray())
						{
							var content = item.GetProperty("content").GetString();
							var correct = item.GetProperty("correct_answer").GetString();
							var rec = await QuestionRepository.CreatePendingFastGptIfNotExistsAsync(null, content!, "blank", null, new[] { correct! }, null);
							created++;
						}
					}
					if (root.TryGetProperty("sort_question", out var sorts) && sorts.ValueKind == System.Text.Json.JsonValueKind.Array)
					{
						foreach (var item in sorts.EnumerateArray())
						{
							var content = item.GetProperty("content").GetString();
							var point = item.TryGetProperty("point", out var p) ? p.GetString() : null;
							var rec = await QuestionRepository.CreatePendingFastGptIfNotExistsAsync(null, content!, "subjective", null, null, point);
							created++;
						}
					}
				}
				catch
				{
					// 如果不是结构化JSON，作为一条主观题
					var rec = await QuestionRepository.CreatePendingIfNotExistsAsync(null, result.Content, "subjective");
					created++;
				}
				return Ok(new { created });
			}
			catch (HttpRequestException ex)
			{
				return StatusCode(502, new { message = "FastGPT upstream error", detail = ex.Message });
			}
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