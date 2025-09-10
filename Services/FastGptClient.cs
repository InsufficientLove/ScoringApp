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
		private readonly FastGptCloudOptions _cloud;
		private readonly ILogger<FastGptClient> _logger;

		public FastGptClient(HttpClient httpClient, IOptions<FastGptOptions> options, IOptions<FastGptCloudOptions> cloudOptions, ILogger<FastGptClient> logger)
		{
			_httpClient = httpClient;
			_options = options.Value;
			_cloud = cloudOptions.Value;
			_logger = logger;
			if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
			{
				_httpClient.BaseAddress = new Uri(_options.BaseUrl);
			}
			_httpClient.Timeout = TimeSpan.FromMinutes(10);
		}

		public record ScoreRequest(string ChatId, string Content, string? CorrectAnswers, string UserAnswer);
		public record ScoreResponse(double Score, string Analysis);
		public record QuestionRequest(string Prompt);
		public record QuestionResponse(string Content);

		private static string CombineUrl(string? baseUrl, string path)
		{
			if (string.IsNullOrWhiteSpace(baseUrl)) return path;
			var trimmed = baseUrl.TrimEnd('/');
			var norm = path.StartsWith("/") ? path : "/" + path;
			return trimmed + norm;
		}

		public async Task<ScoreResponse> ScoreAsync(ScoreRequest request, CancellationToken ct)
		{
			var sw = Stopwatch.StartNew();
			var baseUrl = _options.ScoringApp.BaseUrl ?? _httpClient.BaseAddress?.ToString();
			var uri = CombineUrl(baseUrl, "v1/chat/completions");
			_logger.LogInformation("FastGPT scoring request(v2): requestUri={Uri}, chatId={ChatId}", uri, request.ChatId);
			var apiKey = _options.ScoringApp.ApiKey;
			using var http = new HttpRequestMessage(HttpMethod.Post, uri);
			http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
			http.Headers.Accept.Clear();
			http.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			var payload = JsonSerializer.Serialize(new
			{
				stream = false,
				detail = false,
				chatId = request.ChatId,
				variables = new
				{
					q = request.Content,
					a = request.CorrectAnswers ?? string.Empty
				},
				messages = new object[]
				{
					new { content = request.UserAnswer, role = "user" }
				}
			});
			http.Content = new StringContent(payload, Encoding.UTF8);
			http.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

			using var resp = await _httpClient.SendAsync(http, ct);
			var json = await resp.Content.ReadAsStringAsync(ct);
			if (!resp.IsSuccessStatusCode)
			{
				_logger.LogWarning("FastGPT scoring response error: status={Status}, body={Body}", (int)resp.StatusCode, json);
				resp.EnsureSuccessStatusCode();
			}
			sw.Stop();
			_logger.LogInformation("FastGPT scoring response(v2): elapsedMs={Elapsed}, bytes={Bytes}", sw.ElapsedMilliseconds, json?.Length ?? 0);

			// Response may be a simple JSON with fields {analysis, score}
			using var doc = JsonDocument.Parse(json);
			double score = 0.0;
			string analysis = string.Empty;
			var root = doc.RootElement;
			if (root.ValueKind == JsonValueKind.Object)
			{
				if (root.TryGetProperty("score", out var s) && (s.ValueKind == JsonValueKind.Number || s.ValueKind == JsonValueKind.String))
				{
					if (s.ValueKind == JsonValueKind.Number) score = s.GetDouble();
					else if (double.TryParse(s.GetString(), out var sv)) score = sv; 
				}
				if (root.TryGetProperty("analysis", out var a) && a.ValueKind == JsonValueKind.String)
				{
					analysis = a.GetString() ?? string.Empty;
				}
				// Some providers nest data
				if (score == 0 && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
				{
					if (data.TryGetProperty("score", out var ds) && ds.ValueKind == JsonValueKind.Number) score = ds.GetDouble();
					if (data.TryGetProperty("analysis", out var da) && da.ValueKind == JsonValueKind.String) analysis = da.GetString() ?? analysis;
				}
			}

			return new ScoreResponse(score, analysis);
		}

		public async Task<QuestionResponse> GenerateQuestionAsync(QuestionRequest request, CancellationToken ct)
		{
			var sw = Stopwatch.StartNew();
			var cloudBase = _cloud?.QuestionApp?.BaseUrl;
			var cloudKey = _cloud?.QuestionApp?.ApiKey;
			var baseUrl = string.IsNullOrWhiteSpace(cloudBase) ? (_options.QuestionApp.BaseUrl ?? _httpClient.BaseAddress?.ToString()) : cloudBase;
			var apiKey = string.IsNullOrWhiteSpace(cloudKey) ? _options.QuestionApp.ApiKey : cloudKey;
			var uri = CombineUrl(baseUrl, "v1/chat/completions");
			_logger.LogInformation("FastGPT question request(v2): requestUri={Uri}, promptLength={Len}", uri, request.Prompt?.Length ?? 0);
			using var http = new HttpRequestMessage(HttpMethod.Post, uri);
			http.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
			http.Headers.Accept.Clear();
			http.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
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
			http.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

			try
			{
				using var resp = await _httpClient.SendAsync(http, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct);
				_logger.LogInformation("FastGPT question response headers received: status={Status}", (int)resp.StatusCode);
				var json = await resp.Content.ReadAsStringAsync(ct);
				_logger.LogInformation("FastGPT question response: {Response}", json);

				if (!resp.IsSuccessStatusCode)
				{
					_logger.LogWarning("FastGPT question response error: status={Status}, body={Body}", (int)resp.StatusCode, json);
					throw new HttpRequestException($"FastGPT error {(int)resp.StatusCode}: {json}", null, resp.StatusCode);
				}
				sw.Stop();
				_logger.LogInformation("FastGPT question response(v2): elapsedMs={Elapsed}, bytes={Bytes}", sw.ElapsedMilliseconds, json?.Length ?? 0);
				var snippetLen = Math.Min(json?.Length ?? 0, 1500);
				var snippet = snippetLen > 0 ? json!.Substring(0, snippetLen) : string.Empty;
				_logger.LogInformation("FastGPT question raw response snippet({Len}): {Snippet}", snippetLen, snippet);

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
				content ??= json;

				return new QuestionResponse(content);
			}
			catch (HttpRequestException ex)
			{
				_logger.LogError(ex, "FastGPT question network error: requestUri={RequestUri}", uri);
				throw;
			}
		}
	}
} 