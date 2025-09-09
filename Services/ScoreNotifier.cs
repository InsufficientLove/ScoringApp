using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ScoringApp.Services
{
	public class ScoreNotifier
	{
		private readonly ConcurrentDictionary<string, ConcurrentBag<Channel<string>>> _userChannels = new();
		private readonly ILogger<ScoreNotifier> _logger;

		public ScoreNotifier(ILogger<ScoreNotifier> logger)
		{
			_logger = logger;
		}

		public Channel<string> Subscribe(string userId)
		{
			var channel = Channel.CreateUnbounded<string>();
			var bag = _userChannels.GetOrAdd(userId, _ => new ConcurrentBag<Channel<string>>());
			bag.Add(channel);
			_logger.LogInformation("SSE subscribed: userId={UserId}, channel={ChannelHash}", userId, channel.GetHashCode());
			return channel;
		}

		public void Unsubscribe(string userId, Channel<string> channel)
		{
			if (_userChannels.TryGetValue(userId, out var bag))
			{
				// Channels in ConcurrentBag cannot be removed individually; complete to make readers finish.
				channel.Writer.TryComplete();
				_logger.LogInformation("SSE unsubscribed: userId={UserId}, channel={ChannelHash}", userId, channel.GetHashCode());
			}
		}

		public async Task PublishAsync(string userId, string message)
		{
			if (_userChannels.TryGetValue(userId, out var bag))
			{
				var count = 0;
				foreach (var ch in bag)
				{
					await ch.Writer.WriteAsync(message);
					count++;
				}
				_logger.LogInformation("SSE published: userId={UserId}, subscribers={SubscriberCount}, messageLength={MessageLength}", userId, count, message?.Length ?? 0);
			}
			else
			{
				_logger.LogInformation("SSE publish skipped: userId={UserId}, subscribers=0", userId);
			}
		}
	}
} 