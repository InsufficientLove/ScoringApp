using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Text.Json;

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

		public static async Task<List<QuestionRecord>> ListAllAsync(string? status = null)
		{
			var filter = Builders<QuestionRecord>.Filter.Empty;
			if (!string.IsNullOrWhiteSpace(status))
			{
				filter = Builders<QuestionRecord>.Filter.Eq(x => x.Status, status);
			}
			return await _col.Value.Find(filter).SortByDescending(x => x.UpdatedAt).ThenByDescending(x => x.CreatedAt).Limit(200).ToListAsync();
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

		public static string ComputeNormalizedHash(string content)
		{
			if (string.IsNullOrWhiteSpace(content)) return string.Empty;
			var normalized = content.Replace("\r", "\n").Trim();
			var bytes = Encoding.UTF8.GetBytes(normalized);
			var hash = SHA256.HashData(bytes);
			return Convert.ToHexString(hash);
		}

		static string ComputeCompositeHash(string? title, string content, string? type, string[]? options, string[]? correctAnswers, string? answer)
		{
			var obj = new
			{
				title = title ?? string.Empty,
				content = (content ?? string.Empty).Replace("\r", "\n").Trim(),
				type = type ?? string.Empty,
				options = options ?? Array.Empty<string>(),
				correctAnswers = correctAnswers ?? Array.Empty<string>(),
				answer = answer ?? string.Empty
			};
			var json = JsonSerializer.Serialize(obj);
			return ComputeNormalizedHash(json);
		}

		public static async Task<QuestionRecord?> FindByHashAsync(string hash)
		{
			return await _col.Value.Find(x => x.UniqueHash == hash && x.Status == "approved").FirstOrDefaultAsync();
		}

		public static async Task<QuestionRecord> CreatePendingIfNotExistsAsync(string? title, string content, string? type)
		{
			var hash = ComputeNormalizedHash(content);
			var exists = await _col.Value.Find(x => x.UniqueHash == hash && x.Status == "approved").FirstOrDefaultAsync();
			if (exists != null) return exists;

			var rec = new QuestionRecord
			{
				Id = Guid.NewGuid().ToString("N"),
				UserId = null,
				Title = title,
				Content = content,
				Type = string.IsNullOrWhiteSpace(type) ? "subjective" : type,
				UniqueHash = hash,
				Source = "fastgpt",
				Status = "pending",
				CreatedAt = DateTime.UtcNow
			};
			await _col.Value.InsertOneAsync(rec);
			return rec;
		}

		public static async Task<QuestionRecord> CreatePendingManualAsync(string? title, string content, string? type, string[]? options, string[]? correctAnswers, string? answer)
		{
			var hash = ComputeNormalizedHash(content);
			var rec = new QuestionRecord
			{
				Id = Guid.NewGuid().ToString("N"),
				UserId = null,
				Title = title,
				Content = content,
				Type = string.IsNullOrWhiteSpace(type) ? "subjective" : type,
				Options = options,
				CorrectAnswers = correctAnswers,
				Answer = answer,
				UniqueHash = hash,
				Source = "manual",
				Status = "pending",
				CreatedAt = DateTime.UtcNow
			};
			await _col.Value.InsertOneAsync(rec);
			return rec;
		}

		public static async Task<QuestionRecord> CreatePendingFastGptIfNotExistsAsync(string? title, string content, string? type, string[]? options, string[]? correctAnswers, string? answer)
		{
			var hash = ComputeCompositeHash(title, content, type, options, correctAnswers, answer);
			var exists = await _col.Value.Find(x => x.UniqueHash == hash && x.Status == "approved").FirstOrDefaultAsync();
			if (exists != null) return exists;

			var rec = new QuestionRecord
			{
				Id = Guid.NewGuid().ToString("N"),
				UserId = null,
				Title = title,
				Content = content,
				Type = string.IsNullOrWhiteSpace(type) ? "subjective" : type,
				Options = options,
				CorrectAnswers = correctAnswers,
				Answer = answer,
				UniqueHash = hash,
				Source = "fastgpt",
				Status = "pending",
				CreatedAt = DateTime.UtcNow
			};
			await _col.Value.InsertOneAsync(rec);
			return rec;
		}

		public static async Task ApproveAsync(string id)
		{
			var update = Builders<QuestionRecord>.Update
				.Set(x => x.Status, "approved")
				.Set(x => x.UpdatedAt, DateTime.UtcNow);
			await _col.Value.UpdateOneAsync(x => x.Id == id, update);
		}

		public static async Task<long> ApproveManyAsync(IEnumerable<string> ids)
		{
			var filter = Builders<QuestionRecord>.Filter.In(x => x.Id, ids);
			var update = Builders<QuestionRecord>.Update
				.Set(x => x.Status, "approved")
				.Set(x => x.UpdatedAt, DateTime.UtcNow);
			var result = await _col.Value.UpdateManyAsync(filter, update);
			return result.ModifiedCount;
		}
	}
} 