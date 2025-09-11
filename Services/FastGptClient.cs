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
			var cloudBase = _cloud?.ScoringApp?.BaseUrl;
			var cloudKey = _cloud?.ScoringApp?.ApiKey;
			var baseUrl = string.IsNullOrWhiteSpace(cloudBase) ? _options.ScoringApp.BaseUrl : cloudBase;
			var apiKey = string.IsNullOrWhiteSpace(cloudKey) ? _options.ScoringApp.ApiKey : cloudKey;
			var source = string.IsNullOrWhiteSpace(cloudBase) ? "local" : "cloud";
			if (string.IsNullOrWhiteSpace(baseUrl))
			{
				_logger.LogError("FastGPT scoring BaseUrl not configured. Set FastGptCloud:ScoringApp:BaseUrl or FastGpt:ScoringApp:BaseUrl.");
				throw new InvalidOperationException("FastGPT scoring BaseUrl not configured");
			}
			var uri = CombineUrl(baseUrl, "v1/chat/completions");
			_logger.LogInformation("FastGPT scoring request(v2,{Source}): requestUri={Uri}, chatId={ChatId}", source, uri, request.ChatId);
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
				_logger.LogWarning("FastGPT scoring response error({Source}): status={Status}, body={Body}", source, (int)resp.StatusCode, json);
				resp.EnsureSuccessStatusCode();
			}
			sw.Stop();
			_logger.LogInformation("FastGPT scoring response(v2,{Source}): elapsedMs={Elapsed}, bytes={Bytes}", source, sw.ElapsedMilliseconds, json?.Length ?? 0);

			// Robust parsing: try root {score, analysis}, then data, then JSON strings in content fields
			double? parsedScore = null;
			string? parsedAnalysis = null;

			void TryExtractFrom(JsonElement element)
			{
				if (element.ValueKind != JsonValueKind.Object) return;
				if (element.TryGetProperty("score", out var s))
				{
					if (s.ValueKind == JsonValueKind.Number) parsedScore = s.GetDouble();
					else if (s.ValueKind == JsonValueKind.String && double.TryParse(s.GetString(), out var sv)) parsedScore = sv;
				}
				if (element.TryGetProperty("analysis", out var a) && a.ValueKind == JsonValueKind.String)
				{
					parsedAnalysis = a.GetString();
				}
			}

			string? TryParseJsonString(string? maybeJson)
			{
				if (string.IsNullOrWhiteSpace(maybeJson)) return null;
				try
				{
					using var inner = JsonDocument.Parse(maybeJson);
					TryExtractFrom(inner.RootElement);
					return maybeJson;
				}
				catch
				{
					return null;
				}
			}

			using (var doc = JsonDocument.Parse(json))
			{
				var root = doc.RootElement;
				TryExtractFrom(root);

				if ((parsedScore == null || parsedAnalysis == null) && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
				{
					TryExtractFrom(data);
					if ((parsedScore == null || parsedAnalysis == null) && data.TryGetProperty("content", out var dc) && dc.ValueKind == JsonValueKind.String)
					{
						TryParseJsonString(dc.GetString());
					}
				}

				if ((parsedScore == null || parsedAnalysis == null) && root.TryGetProperty("content", out var c1) && c1.ValueKind == JsonValueKind.String)
				{
					TryParseJsonString(c1.GetString());
				}

				if ((parsedScore == null || parsedAnalysis == null) && root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
				{
					var first = choices[0];
					if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object && msg.TryGetProperty("content", out var c3) && c3.ValueKind == JsonValueKind.String)
					{
						TryParseJsonString(c3.GetString());
					}
				}
			}

			var finalScore = parsedScore ?? 0.0;
			var finalAnalysis = parsedAnalysis ?? string.Empty;

			return new ScoreResponse(finalScore, finalAnalysis);
		}

		public async Task<QuestionResponse> GenerateQuestionAsync(QuestionRequest request, CancellationToken ct)
		{
			var sw = Stopwatch.StartNew();
			var cloudBase = _cloud?.QuestionApp?.BaseUrl;
			var cloudKey = _cloud?.QuestionApp?.ApiKey;
			var baseUrl = string.IsNullOrWhiteSpace(cloudBase) ? _options.QuestionApp.BaseUrl : cloudBase;
			if (string.IsNullOrWhiteSpace(baseUrl))
			{
				_logger.LogError("FastGPT question BaseUrl not configured. Set FastGptCloud:QuestionApp:BaseUrl or FastGpt:QuestionApp:BaseUrl.");
				throw new InvalidOperationException("FastGPT question BaseUrl not configured");
			}
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