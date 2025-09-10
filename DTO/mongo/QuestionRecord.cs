using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace ScoringApp.DTO.mongo
{
	public class QuestionRecord
	{
		[BsonId]
		[BsonRepresentation(BsonType.String)]
		public string Id { get; set; }

		[BsonElement("userId")]
		[BsonIgnoreIfNull]
		public string? UserId { get; set; }

		[BsonElement("title")]
		[BsonIgnoreIfNull]
		public string? Title { get; set; }

		[BsonElement("content")]
		public string Content { get; set; }

		[BsonElement("type")]
		[BsonIgnoreIfNull]
		public string? Type { get; set; } // choice | blank | subjective

		[BsonElement("options")]
		[BsonIgnoreIfNull]
		public string[]? Options { get; set; } // for choice

		[BsonElement("correctAnswers")]
		[BsonIgnoreIfNull]
		public string[]? CorrectAnswers { get; set; } // for choice (multi) and blank answers

		[BsonElement("answer")]
		[BsonIgnoreIfNull]
		public string? Answer { get; set; } // for subjective reference

		[BsonElement("status")]
		[BsonDefaultValue("pending")]
		public string Status { get; set; } = "pending"; // pending|approved|rejected

		[BsonElement("uniqueHash")]
		[BsonIgnoreIfNull]
		public string? UniqueHash { get; set; }

		[BsonElement("source")]
		[BsonIgnoreIfNull]
		public string? Source { get; set; } // e.g., fastgpt

		[BsonElement("createdAt")]
		public DateTime CreatedAt { get; set; }

		[BsonElement("updatedAt")]
		[BsonIgnoreIfNull]
		public DateTime? UpdatedAt { get; set; }
	}
} 