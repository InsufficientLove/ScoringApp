using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoringApp.Config;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace ScoringApp.Services
{
	public class FastGptClient
	{
		private readonly HttpClient _httpClient;
		private readonly FastGptOptions _options;
		private readonly ILogger<FastGptClient> _logger;

		public FastGptClient(HttpClient httpClient, IOptions<FastGptOptions> options, ILogger<FastGptClient> logger)
		{
			_httpClient = httpClient;
			_options = options.Value;
			_logger = logger;
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
			var sw = Stopwatch.StartNew();
			_logger.LogInformation("FastGPT scoring request: appId={AppId}, userId={UserId}, clientAnswerId={ClientAnswerId}", _options.ScoringApp.AppId, request.UserId, request.ClientAnswerId);
			var apiKey = _options.ScoringApp.ApiKey;
			using var http = new HttpRequestMessage(HttpMethod.Post, string.Empty);
			http.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
			var scoreJson = JsonSerializer.Serialize(new
			{
				appId = _options.ScoringApp.AppId,
				messages = new object[]
				{
					new { role = "system", content = "You are a scoring assistant. Return JSON with score:int, feedback:string" },
					new { role = "user", content = new { request.UserId, request.QuestionId, request.Question, request.UserAnswer, request.ClientAnswerId } }
				}
			});
			http.Content = new StringContent(scoreJson, Encoding.UTF8);
			http.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

			using var resp = await _httpClient.SendAsync(http, ct);
			var json = await resp.Content.ReadAsStringAsync(ct);
			resp.EnsureSuccessStatusCode();
			sw.Stop();
			_logger.LogInformation("FastGPT scoring response: elapsedMs={Elapsed}, bytes={Bytes}", sw.ElapsedMilliseconds, json?.Length ?? 0);
			var model = JsonSerializer.Deserialize<ScoreResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			if (model == null) throw new InvalidOperationException("Invalid FastGPT response");
			return model;
		}

		public async Task<QuestionResponse> GenerateQuestionAsync(QuestionRequest request, CancellationToken ct)
		{
			var sw = Stopwatch.StartNew();
			_logger.LogInformation("FastGPT question request(v2): chatCompletions, promptLength={Len}", request.Prompt?.Length ?? 0);
			var apiKey = _options.QuestionApp.ApiKey;
			using var http = new HttpRequestMessage(HttpMethod.Post, string.Empty);
			http.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
			var genJson = JsonSerializer.Serialize(new
			{
				stream = false,
				detail = false,
				chatId = Random.Shared.Next(1, 500001).ToString(),
				messages = new object[]
				{
					new { content = request.Prompt, role = "user" }
				}
			});
			http.Content = new StringContent(genJson, Encoding.UTF8);
			http.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

			using var resp = await _httpClient.SendAsync(http, ct);
			var json = await resp.Content.ReadAsStringAsync(ct);
			if (!resp.IsSuccessStatusCode)
			{
				_logger.LogWarning("FastGPT question response error: status={Status}, body={Body}", (int)resp.StatusCode, json);
				resp.EnsureSuccessStatusCode();
			}
			sw.Stop();
			_logger.LogInformation("FastGPT question response(v2): elapsedMs={Elapsed}, bytes={Bytes}", sw.ElapsedMilliseconds, json?.Length ?? 0);

			// 兼容多种返回结构，尽力提取文本内容
			using var doc = JsonDocument.Parse(json);
			string? content = null;
			var root = doc.RootElement;
			if (root.ValueKind == JsonValueKind.Object)
			{
				if (root.TryGetProperty("content", out var c1) && c1.ValueKind == JsonValueKind.String)
					content = c1.GetString();
				else if (root.TryGetProperty("data", out var data))
				{
					if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("content", out var c2) && c2.ValueKind == JsonValueKind.String)
						content = c2.GetString();
				}
				else if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
				{
					var first = choices[0];
					if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object && msg.TryGetProperty("content", out var c3) && c3.ValueKind == JsonValueKind.String)
						content = c3.GetString();
				}
			}
			content ??= json; // 兜底返回原文

			return new QuestionResponse(content);
		}
	}
} 