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

		// Frontend user id
		[BsonElement("chatId")]
		public string ChatId { get; set; }

		// Question content
		[BsonElement("content")]
		public string Content { get; set; }

		// choice | blank | subjective
		[BsonElement("type")]
		public string Type { get; set; }

		// Correct answers as string (e.g., "A" or "A|B" or text)
		[BsonElement("correctAnswers")]
		[BsonIgnoreIfNull]
		public string? CorrectAnswers { get; set; }

		// User answer as string
		[BsonElement("userAnswer")]
		public string UserAnswer { get; set; }

		// Analysis from scoring model
		[BsonElement("analysis")]
		[BsonIgnoreIfNull]
		public string? Analysis { get; set; }

		// Score as decimal number
		[BsonElement("score")]
		[BsonIgnoreIfNull]
		public double? Score { get; set; }

		// Internal processing fields
		[BsonElement("status")]
		public string Status { get; set; } // pending|processing|done|error

		[BsonElement("error")]
		[BsonIgnoreIfNull]
		public string? Error { get; set; }

		[BsonElement("createdAt")]
		public DateTime CreatedAt { get; set; }

		[BsonElement("updatedAt")]
		[BsonIgnoreIfNull]
		public DateTime? UpdatedAt { get; set; }
	}
} 