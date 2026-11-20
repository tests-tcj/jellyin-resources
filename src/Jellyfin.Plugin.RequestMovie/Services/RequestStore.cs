using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Plugin.RequestMovie.Models;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.RequestMovie.Services;

public sealed class RequestStore
{
	private readonly object _sync = new object();

	private readonly string _path;

	private readonly JsonSerializerOptions _json = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public RequestStore(IApplicationPaths paths)
	{
		Directory.CreateDirectory(paths.PluginConfigurationsPath);
		_path = Path.Combine(paths.PluginConfigurationsPath, "request-movie-requests.json");
	}

	public IReadOnlyList<MediaRequest> List()
	{
		lock (_sync)
		{
			return (from x in Read()
				orderby x.CreatedUtc descending
				select x).ToArray();
		}
	}

	public MediaRequest? AddIfAbsent(NewRequest request, string username, Func<IReadOnlyList<MediaRequest>, bool> isDuplicate)
	{
		lock (_sync)
		{
			List<MediaRequest> list = Read();
			if (isDuplicate(list))
			{
				return null;
			}
			MediaRequest mediaRequest = new MediaRequest
			{
				Title = request.Title.Trim(),
				Kind = request.Kind,
				Year = request.Year,
				TmdbId = (string.IsNullOrWhiteSpace(request.TmdbId) ? null : request.TmdbId.Trim()),
				ImdbId = (string.IsNullOrWhiteSpace(request.ImdbId) ? null : request.ImdbId.Trim())
			};
			string poster = request.Poster;
			mediaRequest.Poster = ((poster != null && poster.Length > 0) ? poster : null);
			mediaRequest.RequestedBy = username;
			MediaRequest mediaRequest2 = mediaRequest;
			list.Add(mediaRequest2);
			Save(list);
			return mediaRequest2;
		}
	}

	public int MarkAvailable(IReadOnlyDictionary<Guid, Guid> identifiers)
	{
		if (identifiers.Count == 0)
		{
			return 0;
		}
		lock (_sync)
		{
			List<MediaRequest> list = Read();
			DateTime utcNow = DateTime.UtcNow;
			int num = 0;
			foreach (MediaRequest item in list)
			{
				if (identifiers.TryGetValue(item.Id, out var value) && (item.Status != "available" || item.JellyfinItemId != value))
				{
					item.Status = "available";
					item.JellyfinItemId = value;
					MediaRequest mediaRequest = item;
					DateTime? availableUtc = mediaRequest.AvailableUtc;
					DateTime valueOrDefault = availableUtc.GetValueOrDefault();
					if (!availableUtc.HasValue)
					{
						valueOrDefault = utcNow;
						DateTime? dateTime = (mediaRequest.AvailableUtc = valueOrDefault);
					}
					item.UpdatedUtc = utcNow;
					num++;
				}
			}
			if (num > 0)
			{
				Save(list);
			}
			return num;
		}
	}

	public bool Delete(Guid id)
	{
		lock (_sync)
		{
			List<MediaRequest> list = Read();
			if (list.RemoveAll((MediaRequest x) => x.Id == id) == 0)
			{
				return false;
			}
			Save(list);
			return true;
		}
	}

	public bool SetStatus(Guid id, string status)
	{
		lock (_sync)
		{
			List<MediaRequest> list = Read();
			MediaRequest mediaRequest = list.FirstOrDefault((MediaRequest x) => x.Id == id);
			if (mediaRequest == null)
			{
				return false;
			}
			mediaRequest.Status = status;
			mediaRequest.UpdatedUtc = DateTime.UtcNow;
			if (status == "available")
			{
				MediaRequest mediaRequest2 = mediaRequest;
				DateTime? availableUtc = mediaRequest2.AvailableUtc;
				DateTime valueOrDefault = availableUtc.GetValueOrDefault();
				if (!availableUtc.HasValue)
				{
					valueOrDefault = mediaRequest.UpdatedUtc;
					DateTime? dateTime = (mediaRequest2.AvailableUtc = valueOrDefault);
				}
			}
			else
			{
				mediaRequest.AvailableUtc = null;
				mediaRequest.JellyfinItemId = null;
			}
			Save(list);
			return true;
		}
	}

	private List<MediaRequest> Read()
	{
		if (!File.Exists(_path))
		{
			return new List<MediaRequest>();
		}
		return JsonSerializer.Deserialize<List<MediaRequest>>(File.ReadAllText(_path), _json) ?? new List<MediaRequest>();
	}

	private void Save(List<MediaRequest> requests)
	{
		string text = _path + ".tmp";
		File.WriteAllText(text, JsonSerializer.Serialize(requests, _json));
		File.Move(text, _path, overwrite: true);
	}
}
