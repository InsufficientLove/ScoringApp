using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace ScoringApp.DTO.mongo
{
	public static class UserRepository
	{
		static readonly Lazy<IMongoCollection<UserRecord>> _col = new Lazy<IMongoCollection<UserRecord>>(() =>
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
			var col = db.GetCollection<UserRecord>("users");
			return col;
		});

		public static async Task<UserRecord?> FindByUsernameAsync(string username)
		{
			return await _col.Value.Find(x => x.Username == username).FirstOrDefaultAsync();
		}

		public static async Task CreateAsync(UserRecord user)
		{
			await _col.Value.InsertOneAsync(user);
		}
	}
} 