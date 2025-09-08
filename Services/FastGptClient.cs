using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ScoringApp.Config;

namespace ScoringApp.Services
{
	public class FastGptClient
	{
		private readonly HttpClient _httpClient;
		private readonly FastGptOptions _options;

		public FastGptClient(HttpClient httpClient, IOptions<FastGptOptions> options)
		{
			_httpClient = httpClient;
			_options = options.Value;
			if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
			{
				_httpClient.BaseAddress = new Uri(_options.BaseUrl);
			}
		}

		public record ScoreRequest(string UserId, string QuestionId, string Question, string UserAnswer, string ClientAnswerId);
		public record ScoreResponse(int Score, string Feedback);
		public record QuestionRequest(string Prompt);
		public record QuestionResponse(string Content);

		public async Task<ScoreResponse> ScoreAsync(ScoreRequest request, CancellationToken ct)
		{
			var apiKey = _options.ScoringApp.ApiKey;
			using var http = new HttpRequestMessage(HttpMethod.Post, string.Empty);
			http.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
			http.Content = new StringContent(JsonSerializer.Serialize(new
			{
				appId = _options.ScoringApp.AppId,
				messages = new object[]
				{
					new { role = "system", content = "You are a scoring assistant. Return JSON with score:int, feedback:string" },
					new { role = "user", content = new { request.UserId, request.QuestionId, request.Question, request.UserAnswer, request.ClientAnswerId } }
				}
			}), Encoding.UTF8, "application/json");

			using var resp = await _httpClient.SendAsync(http, ct);
			resp.EnsureSuccessStatusCode();
			var json = await resp.Content.ReadAsStringAsync(ct);
			// NOTE: 根据实际 FastGPT 返回格式解析。此处假设直接返回 { score, feedback }
			var model = JsonSerializer.Deserialize<ScoreResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			if (model == null) throw new InvalidOperationException("Invalid FastGPT response");
			return model;
		}

		public async Task<QuestionResponse> GenerateQuestionAsync(QuestionRequest request, CancellationToken ct)
		{
			var apiKey = _options.QuestionApp.ApiKey;
			using var http = new HttpRequestMessage(HttpMethod.Post, string.Empty);
			http.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
			http.Content = new StringContent(JsonSerializer.Serialize(new
			{
				appId = _options.QuestionApp.AppId,
				messages = new object[]
				{
					new { role = "system", content = "You are a question generator. Return plain text question." },
					new { role = "user", content = request.Prompt }
				}
			}), Encoding.UTF8, "application/json");

			using var resp = await _httpClient.SendAsync(http, ct);
			resp.EnsureSuccessStatusCode();
			var json = await resp.Content.ReadAsStringAsync(ct);
			// NOTE: 根据实际 FastGPT 返回格式解析。此处假设返回 { content }
			var model = JsonSerializer.Deserialize<QuestionResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			if (model == null) throw new InvalidOperationException("Invalid FastGPT response");
			return model;
		}
	}
} 