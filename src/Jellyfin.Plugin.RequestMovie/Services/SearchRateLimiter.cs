using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.RequestMovie.Services;

public sealed class SearchRateLimiter
{
	private readonly object _sync = new object();

	private readonly Dictionary<string, (DateTimeOffset Started, int Count)> _counts = new Dictionary<string, (DateTimeOffset, int)>(StringComparer.Ordinal);

	public (bool Allowed, int RetryAfterSeconds) TryAcquire(string userId, int maxPerHour)
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		maxPerHour = Math.Clamp(maxPerHour, 1, 200);
		lock (_sync)
		{
			if (_counts.Count > 1000)
			{
				string[] array = (from x in _counts
					where now - x.Value.Started >= TimeSpan.FromHours(1)
					select x.Key).ToArray();
				foreach (string key in array)
				{
					_counts.Remove(key);
				}
			}
			if (!_counts.TryGetValue(userId, out (DateTimeOffset, int) value) || now - value.Item1 >= TimeSpan.FromHours(1))
			{
				_counts[userId] = (now, 1);
				return (Allowed: true, RetryAfterSeconds: 0);
			}
			if (value.Item2 >= maxPerHour)
			{
				int item = Math.Max(1, (int)Math.Ceiling((value.Item1.AddHours(1.0) - now).TotalSeconds));
				return (Allowed: false, RetryAfterSeconds: item);
			}
			_counts[userId] = (value.Item1, value.Item2 + 1);
			return (Allowed: true, RetryAfterSeconds: 0);
		}
	}
}
