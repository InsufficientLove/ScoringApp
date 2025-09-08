using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ScoringApp.Services
{
	public class ScoreNotifier
	{
		private readonly ConcurrentDictionary<string, ConcurrentBag<Channel<string>>> _userChannels = new();

		public Channel<string> Subscribe(string userId)
		{
			var channel = Channel.CreateUnbounded<string>();
			var bag = _userChannels.GetOrAdd(userId, _ => new ConcurrentBag<Channel<string>>());
			bag.Add(channel);
			return channel;
		}

		public void Unsubscribe(string userId, Channel<string> channel)
		{
			if (_userChannels.TryGetValue(userId, out var bag))
			{
				// Channels in ConcurrentBag cannot be removed individually; complete to make readers finish.
				channel.Writer.TryComplete();
			}
		}

		public async Task PublishAsync(string userId, string message)
		{
			if (_userChannels.TryGetValue(userId, out var bag))
			{
				foreach (var ch in bag)
				{
					await ch.Writer.WriteAsync(message);
				}
			}
		}
	}
} 