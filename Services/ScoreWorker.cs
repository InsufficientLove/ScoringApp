using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScoringApp.DTO.mongo;

namespace ScoringApp.Services
{
	public class ScoreWorker : BackgroundService
	{
		private readonly ILogger<ScoreWorker> _logger;
		private readonly FastGptClient _fastGptClient;
		private readonly ScoreNotifier _notifier;

		public ScoreWorker(ILogger<ScoreWorker> logger, FastGptClient fastGptClient, ScoreNotifier notifier)
		{
			_logger = logger;
			_fastGptClient = fastGptClient;
			_notifier = notifier;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("ScoreWorker started");
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					var rec = await ScoreRepository.TryAcquirePendingAsync(stoppingToken);
					if (rec == null)
					{
						await Task.Delay(1000, stoppingToken);
						continue;
					}

					try
					{
						var request = new FastGptClient.ScoreRequest(rec.UserId, rec.QuestionId, rec.Question, rec.UserAnswer, rec.ClientAnswerId);
						var response = await _fastGptClient.ScoreAsync(request, stoppingToken);
						await ScoreRepository.UpdateDoneAsync(rec.Id, response.Score, response.Feedback, stoppingToken);

						var payload = JsonSerializer.Serialize(new
						{
							type = "done",
							rec.ClientAnswerId,
							score = response.Score,
							feedback = response.Feedback,
							updatedAt = DateTime.UtcNow
						});
						await _notifier.PublishAsync(rec.UserId, payload);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Scoring failed for record {RecordId}", rec.Id);
						await ScoreRepository.UpdateErrorAsync(rec.Id, ex.Message, stoppingToken);
						var payload = JsonSerializer.Serialize(new
						{
							type = "error",
							rec.ClientAnswerId,
							error = ex.Message,
							updatedAt = DateTime.UtcNow
						});
						await _notifier.PublishAsync(rec.UserId, payload);
					}
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Worker loop error");
					await Task.Delay(2000, stoppingToken);
				}
			}
		}
	}
} 