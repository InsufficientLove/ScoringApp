using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace ScoringApp.DTO.mongo
{
	public class ScoreRecord
	{
		[BsonId]
		[BsonRepresentation(BsonType.String)]
		public string Id { get; set; }

		[BsonElement("userId")]
		public string UserId { get; set; }

		[BsonElement("questionId")]
		public string QuestionId { get; set; }

		[BsonElement("question")]
		public string Question { get; set; }

		[BsonElement("userAnswer")]
		public string UserAnswer { get; set; }

		[BsonElement("clientAnswerId")]
		public string ClientAnswerId { get; set; }

		[BsonElement("status")]
		public string Status { get; set; } // pending|done|error

		[BsonElement("score")]
		[BsonIgnoreIfNull]
		public int? Score { get; set; }

		[BsonElement("feedback")]
		[BsonIgnoreIfNull]
		public string Feedback { get; set; }

		[BsonElement("error")]
		[BsonIgnoreIfNull]
		public string Error { get; set; }

		[BsonElement("createdAt")]
		public DateTime CreatedAt { get; set; }

		[BsonElement("updatedAt")]
		[BsonIgnoreIfNull]
		public DateTime? UpdatedAt { get; set; }
	}
} 