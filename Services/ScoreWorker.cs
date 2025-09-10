using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
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

					_logger.LogInformation("Acquire pending: id={Id}, chatId={ChatId}, type={Type}", rec.Id, rec.ChatId, rec.Type);

					try
					{
						var sw = Stopwatch.StartNew();
						var request = new FastGptClient.ScoreRequest(rec.ChatId, rec.Content, rec.CorrectAnswers, rec.UserAnswer);
						_logger.LogInformation("Scoring start: chatId={ChatId}, id={Id}", rec.ChatId, rec.Id);
						var response = await _fastGptClient.ScoreAsync(request, stoppingToken);
						sw.Stop();
						await ScoreRepository.UpdateDoneAsync(rec.Id, response.Score, response.Analysis, stoppingToken);
						_logger.LogInformation("Scoring done: chatId={ChatId}, id={Id}, score={Score}, elapsedMs={Elapsed}", rec.ChatId, rec.Id, response.Score, sw.ElapsedMilliseconds);

						var payload = JsonSerializer.Serialize(new
						{
							type = "done",
							id = rec.Id,
							score = response.Score,
							analysis = response.Analysis,
							updatedAt = DateTime.UtcNow
						});
						await _notifier.PublishAsync(rec.ChatId, payload);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Scoring failed: id={Id}, chatId={ChatId}", rec.Id, rec.ChatId);
						await ScoreRepository.UpdateErrorAsync(rec.Id, ex.Message, stoppingToken);
						var payload = JsonSerializer.Serialize(new
						{
							type = "error",
							id = rec.Id,
							error = ex.Message,
							updatedAt = DateTime.UtcNow
						});
						await _notifier.PublishAsync(rec.ChatId, payload);
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