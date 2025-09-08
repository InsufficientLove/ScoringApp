using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ScoringApp.Services
{
	public class FastGptClient
	{
		private readonly HttpClient _httpClient;
		private readonly string _apiKey;

		public FastGptClient(HttpClient httpClient)
		{
			_httpClient = httpClient;
			_apiKey = Environment.GetEnvironmentVariable("FASTGPT_API_KEY") ?? string.Empty;
			if (!string.IsNullOrEmpty(_apiKey))
			{
				_httpClient.DefaultRequestHeaders.Remove("Authorization");
				_httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
			}
		}

		public record ScoreRequest(string UserId, string QuestionId, string Question, string UserAnswer, string ClientAnswerId);
		public record ScoreResponse(int Score, string Feedback);

		public async Task<ScoreResponse> ScoreAsync(ScoreRequest request, CancellationToken ct)
		{
			using var httpResponse = await _httpClient.PostAsJsonAsync("api/score", request, ct);
			httpResponse.EnsureSuccessStatusCode();
			var json = await httpResponse.Content.ReadAsStringAsync(ct);
			var model = JsonSerializer.Deserialize<ScoreResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			if (model == null) throw new InvalidOperationException("Invalid FastGPT response");
			return model;
		}
	}
} 