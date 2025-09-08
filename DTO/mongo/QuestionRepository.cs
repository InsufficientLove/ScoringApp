using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScoringApp.DTO.mongo
{
	public static class QuestionRepository
	{
		static readonly Lazy<IMongoCollection<QuestionRecord>> _col = new Lazy<IMongoCollection<QuestionRecord>>(() =>
		{
			var host = Environment.GetEnvironmentVariable("MONGO_HOST") ?? "192.168.20.250";
			var port = Environment.GetEnvironmentVariable("MONGO_PORT") ?? "27018";
			var dbName = Environment.GetEnvironmentVariable("MONGO_DB") ?? "AITraining";
			var authDb = Environment.GetEnvironmentVariable("MONGO_AUTH_DB") ?? "admin";
			var user = Environment.GetEnvironmentVariable("MONGO_USER") ?? "mongo";
			var password = Environment.GetEnvironmentVariable("MONGO_PASSWORD") ?? "mongo";
			var settings = MongoClientSettings.FromConnectionString($"mongodb://{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(password)}@{host}:{port}/?authSource={authDb}");
			var client = new MongoClient(settings);
			var db = client.GetDatabase(dbName);
			return db.GetCollection<QuestionRecord>("questions");
		});

		public static async Task<List<QuestionRecord>> ListByUserAsync(string userId)
		{
			return await _col.Value.Find(x => x.UserId == userId).SortByDescending(x => x.UpdatedAt).ToListAsync();
		}

		public static async Task<QuestionRecord?> FindByIdAsync(string id)
		{
			return await _col.Value.Find(x => x.Id == id).FirstOrDefaultAsync();
		}

		public static async Task CreateAsync(QuestionRecord q)
		{
			await _col.Value.InsertOneAsync(q);
		}

		public static async Task UpdateAsync(string id, string? title, string content, string? type, string[]? options, string[]? correctAnswers, string? answer)
		{
			var update = Builders<QuestionRecord>.Update
				.Set(x => x.Title, title)
				.Set(x => x.Content, content)
				.Set(x => x.Type, type)
				.Set(x => x.Options, options)
				.Set(x => x.CorrectAnswers, correctAnswers)
				.Set(x => x.Answer, answer)
				.Set(x => x.UpdatedAt, DateTime.UtcNow);
			await _col.Value.UpdateOneAsync(x => x.Id == id, update);
		}

		public static async Task DeleteAsync(string id)
		{
			await _col.Value.DeleteOneAsync(x => x.Id == id);
		}
	}
} 