namespace ScoringApp.Config
{
	public class FastGptOptions
	{
		public string BaseUrl { get; set; } = string.Empty;
		public AppOptions QuestionApp { get; set; } = new AppOptions();
		public AppOptions ScoringApp { get; set; } = new AppOptions();

		public class AppOptions
		{
			public string AppId { get; set; } = string.Empty;
			public string ApiKey { get; set; } = string.Empty;
			public string BaseUrl { get; set; } = string.Empty;
		}
	}

	public class FastGptCloudOptions
	{
		public class AppOptions
		{
			public string AppId { get; set; } = string.Empty;
			public string ApiKey { get; set; } = string.Empty;
			public string BaseUrl { get; set; } = string.Empty;
		}

		public AppOptions QuestionApp { get; set; } = new AppOptions();
	}
} 