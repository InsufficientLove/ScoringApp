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
			_logger.LogInformation("FastGPT question request: appId={AppId}", _options.QuestionApp.AppId);
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
			var json = await resp.Content.ReadAsStringAsync(ct);
			resp.EnsureSuccessStatusCode();
			sw.Stop();
			_logger.LogInformation("FastGPT question response: elapsedMs={Elapsed}, bytes={Bytes}", sw.ElapsedMilliseconds, json?.Length ?? 0);
			var model = JsonSerializer.Deserialize<QuestionResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			if (model == null) throw new InvalidOperationException("Invalid FastGPT response");
			return model;
		}
	}
} 