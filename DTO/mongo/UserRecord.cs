using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace ScoringApp.DTO.mongo
{
	public class UserRecord
	{
		[BsonId]
		[BsonRepresentation(BsonType.String)]
		public string Id { get; set; }

		[BsonElement("username")]
		public string Username { get; set; }

		[BsonElement("passwordHash")]
		public string PasswordHash { get; set; }

		[BsonElement("createdAt")]
		public DateTime CreatedAt { get; set; }

		[BsonElement("updatedAt")]
		[BsonIgnoreIfNull]
		public DateTime? UpdatedAt { get; set; }
	}
} 