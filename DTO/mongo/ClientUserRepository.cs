using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace ScoringApp.DTO.mongo
{
	public static class ClientUserRepository
	{
		static readonly Lazy<IMongoCollection<ClientUserRecord>> _col = new Lazy<IMongoCollection<ClientUserRecord>>(() =>
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
			return db.GetCollection<ClientUserRecord>("client_users");
		});

		public static async Task<ClientUserRecord?> FindByUsernameAsync(string username)
		{
			return await _col.Value.Find(x => x.Username == username).FirstOrDefaultAsync();
		}

		public static async Task CreateAsync(ClientUserRecord user)
		{
			await _col.Value.InsertOneAsync(user);
		}
	}
} 