using MongoDB.Driver;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace ScoringApp.DTO.mongo
{
	public static class ScoreRepository
	{
		static readonly Lazy<IMongoCollection<ScoreRecord>> _col = new Lazy<IMongoCollection<ScoreRecord>>(() =>
		{
			var host = Environment.GetEnvironmentVariable("MONGO_HOST") ?? "192.168.20.250";
			var port = Environment.GetEnvironmentVariable("MONGO_PORT") ?? "27018";
			var dbName = Environment.GetEnvironmentVariable("MONGO_DB") ?? "AITraining";
			var authDb = Environment.GetEnvironmentVariable("MONGO_AUTH_DB") ?? "admin";
			var user = Environment.GetEnvironmentVariable("MONGO_USER") ?? "mongo";
			var password = Environment.GetEnvironmentVariable("MONGO_PASSWORD") ?? "mongo";
			var collection = Environment.GetEnvironmentVariable("MONGO_COLLECTION") ?? "scores";

			var settings = MongoClientSettings.FromConnectionString($"mongodb://{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(password)}@{host}:{port}/?authSource={authDb}");
			settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
			var client = new MongoClient(settings);
			var db = client.GetDatabase(dbName);
			return db.GetCollection<ScoreRecord>(collection);
		});

		public static async Task EnqueuePendingAsync(string userId, string questionId, string questionText, string userAnswer, string clientAnswerId)
		{
			var rec = new ScoreRecord
			{
				Id = Guid.NewGuid().ToString("N"),
				UserId = userId,
				QuestionId = questionId,
				Question = questionText,
				UserAnswer = userAnswer,
				ClientAnswerId = clientAnswerId,
				Status = "pending",
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = null
			};
			await _col.Value.InsertOneAsync(rec);
		}

		public static async Task<ScoreRecord?> TryAcquirePendingAsync(CancellationToken ct)
		{
			var filter = Builders<ScoreRecord>.Filter.Eq(x => x.Status, "pending");
			var update = Builders<ScoreRecord>.Update
				.Set(x => x.Status, "processing")
				.Set(x => x.UpdatedAt, DateTime.UtcNow);
			var options = new FindOneAndUpdateOptions<ScoreRecord>
			{
				IsUpsert = false,
				ReturnDocument = ReturnDocument.After,
				Sort = Builders<ScoreRecord>.Sort.Ascending(x => x.CreatedAt)
			};
			return await _col.Value.FindOneAndUpdateAsync(filter, update, options, ct);
		}

		public static async Task UpdateDoneAsync(string id, int score, string feedback, CancellationToken ct)
		{
			var update = Builders<ScoreRecord>.Update
				.Set(x => x.Status, "done")
				.Set(x => x.Score, score)
				.Set(x => x.Feedback, feedback)
				.Set(x => x.Error, null)
				.Set(x => x.UpdatedAt, DateTime.UtcNow);
			await _col.Value.UpdateOneAsync(x => x.Id == id, update, cancellationToken: ct);
		}

		public static async Task UpdateErrorAsync(string id, string error, CancellationToken ct)
		{
			var update = Builders<ScoreRecord>.Update
				.Set(x => x.Status, "error")
				.Set(x => x.Error, error)
				.Set(x => x.UpdatedAt, DateTime.UtcNow);
			await _col.Value.UpdateOneAsync(x => x.Id == id, update, cancellationToken: ct);
		}

		public static async Task<ScoreRecord?> FindLatestDoneByUserAsync(string userId, CancellationToken ct)
		{
			return await _col.Value
				.Find(x => x.UserId == userId && x.Status == "done")
				.SortByDescending(x => x.UpdatedAt)
				.FirstOrDefaultAsync(ct);
		}

		public static async Task<ScoreRecord?> FindByClientAnswerIdAsync(string clientAnswerId, CancellationToken ct)
		{
			return await _col.Value
				.Find(x => x.ClientAnswerId == clientAnswerId)
				.FirstOrDefaultAsync(ct);
		}
	}
} 