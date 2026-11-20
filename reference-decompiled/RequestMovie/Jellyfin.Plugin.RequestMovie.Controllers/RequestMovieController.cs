using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RequestMovie.Configuration;
using Jellyfin.Plugin.RequestMovie.Models;
using Jellyfin.Plugin.RequestMovie.Services;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RequestMovie.Controllers;

[ApiController]
[Route("RequestMovie")]
[Authorize]
public sealed class RequestMovieController(RequestStore store, LibraryChecks library, SearchRateLimiter searchRateLimiter, IHttpClientFactory httpClientFactory) : ControllerBase
{
	private string Username => User.Identity?.Name ?? User.FindFirst("name")?.Value ?? User.FindFirst("JellyfinUserId")?.Value ?? "Jellyfin user";

	private static string? Validate(NewRequest input)
	{
		if (!string.IsNullOrWhiteSpace(input.Title))
		{
			int length = input.Title.Length;
			if (length >= 2 && length <= 160)
			{
				string kind = input.Kind;
				if (!(kind == "movie") && !(kind == "series"))
				{
					return "Tip invalid.";
				}
				if (input.Year.HasValue && (input.Year < 1880 || input.Year > 2100))
				{
					return "An invalid.";
				}
				string tmdbId = input.TmdbId;
				if (tmdbId != null && tmdbId.Length > 0 && (tmdbId.Length > 20 || !tmdbId.All(char.IsDigit)))
				{
					return "TMDb ID invalid.";
				}
				string imdbId = input.ImdbId;
				if (imdbId != null && imdbId.Length > 0 && !Regex.IsMatch(imdbId, "^tt[0-9]{7,12}$", RegexOptions.IgnoreCase))
				{
					return "IMDb ID invalid.";
				}
				string poster = input.Poster;
				if (poster != null && poster.Length > 0 && (poster.Length > 300 || !poster.StartsWith("https://image.tmdb.org/t/p/", StringComparison.Ordinal)))
				{
					return "Poster URL invalid.";
				}
				return null;
			}
		}
		return "Titlul trebuie sa aiba intre 2 si 160 caractere.";
	}

	private async Task<string?> FindImdbIdAsync(string kind, string? tmdbId, CancellationToken token)
	{
		if (string.IsNullOrWhiteSpace(tmdbId))
		{
			return null;
		}
		string text = ((BasePlugin<PluginConfiguration>)(object)Plugin.Instance)?.Configuration.TmdbApiKey;
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		string value = ((kind == "series") ? "tv" : "movie");
		string requestUri = $"{value}/{tmdbId}/external_ids?api_key={Uri.EscapeDataString(text)}";
		try
		{
			using HttpResponseMessage response = await httpClientFactory.CreateClient("RequestMovie.Tmdb").GetAsync(requestUri, token).ConfigureAwait(continueOnCapturedContext: false);
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}
			using JsonDocument jsonDocument = await response.Content.ReadFromJsonAsync<JsonDocument>(token).ConfigureAwait(continueOnCapturedContext: false);
			if (jsonDocument == null)
			{
				return null;
			}
			JsonElement value2;
			return jsonDocument.RootElement.TryGetProperty("imdb_id", out value2) ? value2.GetString() : null;
		}
		catch (HttpRequestException)
		{
			return null;
		}
		catch (TaskCanceledException) when (!token.IsCancellationRequested)
		{
			return null;
		}
	}

	[AllowAnonymous]
	[HttpGet("Nav.js")]
	public IActionResult NavigationScript()
	{
		using Stream stream = typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.RequestMovie.Client.requestMovieNav.js");
		if (stream == null)
		{
			return NotFound();
		}
		using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
		Response.Headers.CacheControl = "no-cache, no-store";
		return Content(streamReader.ReadToEnd(), "text/javascript", Encoding.UTF8);
	}

	[HttpGet("Ui")]
	public IActionResult UserInterface()
	{
		using Stream stream = typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.RequestMovie.Client.requestMovieUi.html");
		if (stream == null)
		{
			return NotFound();
		}
		using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
		Response.Headers.CacheControl = "no-cache, no-store";
		return Content(streamReader.ReadToEnd(), "text/html", Encoding.UTF8);
	}

	[HttpGet("Requests")]
	public ActionResult<IReadOnlyList<MediaRequest>> GetRequests()
	{
		IReadOnlyDictionary<Guid, Guid> identifiers = library.FindAvailable(store.List());
		store.MarkAvailable(identifiers);
		return Ok(store.List());
	}

	[HttpGet("Check")]
	public async Task<IActionResult> Check([FromQuery] string title, [FromQuery] string kind = "movie", [FromQuery] int? year = null, [FromQuery] string? tmdbId = null, [FromQuery] string? imdbId = null)
	{
		NewRequest input = new NewRequest(title ?? "", kind, year, tmdbId, imdbId);
		string text = Validate(input);
		if (text != null)
		{
			return BadRequest(new
			{
				error = text
			});
		}
		if (string.IsNullOrWhiteSpace(input.ImdbId) && !string.IsNullOrWhiteSpace(input.TmdbId))
		{
			NewRequest newRequest = input with { };
			NewRequest newRequest2 = newRequest;
			newRequest2.ImdbId = await FindImdbIdAsync(kind, input.TmdbId, HttpContext.RequestAborted);
			input = newRequest;
		}
		BaseItem val = library.Match(input);
		if (val != null)
		{
			return Ok(new
			{
				state = "available",
				jellyfinItemId = val.Id.ToString("N")
			});
		}
		if (store.List().Any((MediaRequest x) =>
		{
			switch (x.Status)
			{
			case "pending":
			case "approved":
			case "available":
				return LibraryChecks.Same(input, x);
			default:
				return false;
			}
		}))
		{
			return Ok(new
			{
				state = "requested"
			});
		}
		return Ok(new
		{
			state = "new"
		});
	}

	[HttpPost("Requests")]
	public async Task<IActionResult> Create([FromBody] NewRequest input)
	{
		Plugin? instance = Plugin.Instance;
		if (instance == null || !((BasePlugin<PluginConfiguration>)(object)instance).Configuration.AllowRequests)
		{
			return BadRequest(new
			{
				error = "Cererile sunt dezactivate."
			});
		}
		string text = Validate(input);
		if (text != null)
		{
			return BadRequest(new
			{
				error = text
			});
		}
		if (string.IsNullOrWhiteSpace(input.ImdbId) && !string.IsNullOrWhiteSpace(input.TmdbId))
		{
			NewRequest newRequest = input with { };
			NewRequest newRequest2 = newRequest;
			newRequest2.ImdbId = await FindImdbIdAsync(input.Kind, input.TmdbId, HttpContext.RequestAborted);
			input = newRequest;
		}
		if (library.Exists(input))
		{
			return Conflict(new
			{
				state = "available"
			});
		}
		MediaRequest mediaRequest = store.AddIfAbsent(input, Username, (IReadOnlyList<MediaRequest> all) => all.Any((MediaRequest x) =>
		{
			switch (x.Status)
			{
			case "pending":
			case "approved":
			case "available":
				return LibraryChecks.Same(input, x);
			default:
				return false;
			}
		}));
		if (mediaRequest == null)
		{
			return Conflict(new
			{
				state = "requested"
			});
		}
		return Ok(mediaRequest);
	}

	private static bool TryBuildAdminActionUrl(MediaRequest request, string template, out string url)
	{
		url = string.Empty;
		if (string.IsNullOrWhiteSpace(template) || template.Length > 2048 || template.IndexOfAny(new char[2] { '\r', '\n' }) >= 0)
		{
			return false;
		}
		Dictionary<string, string> variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["title"] = request.Title,
			["year"] = request.Year?.ToString(CultureInfo.InvariantCulture) ?? "",
			["tmdbId"] = request.TmdbId ?? "",
			["imdbId"] = request.ImdbId ?? "",
			["kind"] = request.Kind,
			["requestId"] = request.Id.ToString("D")
		};
		string text = Regex.Replace(template, "\\{([a-zA-Z][a-zA-Z0-9]*)\\}", (Match match) => (!variables.TryGetValue(match.Groups[1].Value, out string value)) ? match.Value : Uri.EscapeDataString(value ?? ""));
		if (!text.Contains('{') && !text.Contains('}') && text.Length <= 4096 && Uri.TryCreate(text, UriKind.Absolute, out Uri result))
		{
			string scheme = result.Scheme;
			if ((scheme == "https" || scheme == "http") && !string.IsNullOrWhiteSpace(result.Host) && string.IsNullOrEmpty(result.UserInfo))
			{
				url = result.AbsoluteUri;
				return true;
			}
		}
		return false;
	}

	[HttpGet("Admin/Capabilities")]
	[Authorize(Policy = "RequiresElevation")]
	public IActionResult AdminCapabilities()
	{
		PluginConfiguration pluginConfiguration = ((BasePlugin<PluginConfiguration>)(object)Plugin.Instance)?.Configuration;
		string text = pluginConfiguration?.AdminActionButtonLabel?.Trim();
		bool actionEnabled = !string.IsNullOrWhiteSpace(pluginConfiguration?.AdminActionUrlTemplate);
		return Ok(new
		{
			canDelete = true,
			actionEnabled = actionEnabled,
			actionLabel = (string.IsNullOrWhiteSpace(text) ? "Deschide" : text.Substring(0, Math.Min(text.Length, 60)))
		});
	}

	[HttpGet("Admin/Action/{id:guid}/Url")]
	[Authorize(Policy = "RequiresElevation")]
	public IActionResult AdminActionUrl(Guid id)
	{
		MediaRequest mediaRequest = store.List().FirstOrDefault((MediaRequest x) => x.Id == id);
		if (mediaRequest == null)
		{
			return NotFound();
		}
		if (mediaRequest.Kind != "movie" || mediaRequest.Status != "pending")
		{
			return BadRequest(new
			{
				error = "Actiunea este permisa doar pentru filme in asteptare."
			});
		}
		string template = ((BasePlugin<PluginConfiguration>)(object)Plugin.Instance)?.Configuration.AdminActionUrlTemplate ?? "";
		if (!TryBuildAdminActionUrl(mediaRequest, template, out string url))
		{
			return BadRequest(new
			{
				error = "URL-ul configurat pentru butonul de administrator este invalid."
			});
		}
		return Ok(new { url });
	}

	[HttpDelete("Requests/{id:guid}")]
	[Authorize(Policy = "RequiresElevation")]
	public IActionResult Delete(Guid id)
	{
		if (!store.Delete(id))
		{
			return NotFound();
		}
		return NoContent();
	}

	[HttpPatch("Requests/{id:guid}")]
	[Authorize(Policy = "RequiresElevation")]
	public IActionResult SetStatus(Guid id, [FromBody] StatusChange input)
	{
		bool flag;
		switch (input.Status)
		{
		case "pending":
		case "approved":
		case "rejected":
		case "available":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return BadRequest(new
			{
				error = "Status invalid."
			});
		}
		if (!store.SetStatus(id, input.Status))
		{
			return NotFound();
		}
		return NoContent();
	}

	private static object MapTmdbItem(JsonElement item, string kind, string? knownImdbId = null)
	{
		string propertyName = ((kind == "series") ? "name" : "title");
		string propertyName2 = ((kind == "series") ? "first_air_date" : "release_date");
		string? title = ((item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String) ? (value.GetString() ?? "") : "");
		string text = ((item.TryGetProperty(propertyName2, out var value2) && value2.ValueKind == JsonValueKind.String) ? (value2.GetString() ?? "") : "");
		string text2 = ((item.TryGetProperty("poster_path", out var value3) && value3.ValueKind == JsonValueKind.String) ? value3.GetString() : null);
		string overview = ((item.TryGetProperty("overview", out var value4) && value4.ValueKind == JsonValueKind.String) ? value4.GetString() : null);
		string text3 = knownImdbId;
		if (string.IsNullOrWhiteSpace(text3) && item.TryGetProperty("imdb_id", out var value5) && value5.ValueKind == JsonValueKind.String)
		{
			text3 = value5.GetString();
		}
		int result;
		return new
		{
			title = title,
			kind = kind,
			year = ((text.Length >= 4 && int.TryParse(text.Substring(0, 4), out result)) ? new int?(result) : ((int?)null)),
			tmdbId = item.GetProperty("id").ToString(),
			imdbId = text3,
			poster = (string.IsNullOrEmpty(text2) ? null : ("https://image.tmdb.org/t/p/w185" + text2)),
			overview = overview
		};
	}

	[HttpGet("Search")]
	public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] string kind = "movie", CancellationToken cancellationToken = default(CancellationToken))
	{
		query = query?.Trim() ?? "";
		if (!(kind == "movie") && !(kind == "series"))
		{
			return BadRequest(new
			{
				error = "Tip invalid."
			});
		}
		Match imdbMatch = Regex.Match(query, "^tt[0-9]{7,12}$", RegexOptions.IgnoreCase);
		Match tmdbMatch = Regex.Match(query, "^(?:tmdb\\s*:\\s*)?([0-9]{1,12})$", RegexOptions.IgnoreCase);
		if (!imdbMatch.Success && !tmdbMatch.Success && (query.Length < 2 || query.Length > 120))
		{
			return BadRequest(new
			{
				error = "Cautarea trebuie sa contina 2-120 caractere."
			});
		}
		if (query.Length > 120)
		{
			return BadRequest(new
			{
				error = "Cautare prea lunga."
			});
		}
		string text = ((BasePlugin<PluginConfiguration>)(object)Plugin.Instance)?.Configuration.TmdbApiKey;
		if (string.IsNullOrWhiteSpace(text))
		{
			return BadRequest(new
			{
				error = "Configureaza cheia TMDb in plugin."
			});
		}
		string userId = User.FindFirst("JellyfinUserId")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ?? Username;
		int num = Math.Clamp(((BasePlugin<PluginConfiguration>)(object)Plugin.Instance)?.Configuration.SearchesPerUserPerHour ?? 30, 1, 200);
		(bool, int) tuple = searchRateLimiter.TryAcquire(userId, num);
		if (!tuple.Item1)
		{
			Response.Headers["Retry-After"] = tuple.Item2.ToString(CultureInfo.InvariantCulture);
			return StatusCode(429, new
			{
				error = $"Limita de {num} cautari pe ora a fost atinsa. Incearca mai tarziu.",
				retryAfterSeconds = tuple.Item2
			});
		}
		string value = ((kind == "series") ? "tv" : "movie");
		string value2 = Uri.EscapeDataString(text);
		string requestUri;
		if (imdbMatch.Success)
		{
			requestUri = $"find/{imdbMatch.Value.ToLowerInvariant()}?api_key={value2}&external_source=imdb_id&language=ro-RO";
		}
		else
		{
			requestUri = (tmdbMatch.Success ? $"{value}/{tmdbMatch.Groups[1].Value}?api_key={value2}&language=ro-RO" : $"search/{value}?api_key={value2}&language=ro-RO&query={Uri.EscapeDataString(query)}");
		}
		try
		{
			using HttpResponseMessage response = await httpClientFactory.CreateClient("RequestMovie.Tmdb").GetAsync(requestUri, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (response.StatusCode == HttpStatusCode.NotFound && (imdbMatch.Success || tmdbMatch.Success))
			{
				return Ok(Array.Empty<object>());
			}
			if (!response.IsSuccessStatusCode)
			{
				return StatusCode(502, new
				{
					error = "TMDb a returnat o eroare."
				});
			}
			using JsonDocument jsonDocument = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (jsonDocument == null)
			{
				return Ok(Array.Empty<object>());
			}
			List<object> list = new List<object>();
			if (tmdbMatch.Success)
			{
				list.Add(MapTmdbItem(jsonDocument.RootElement, kind));
			}
			else
			{
				string propertyName;
				if (imdbMatch.Success)
				{
					propertyName = ((kind == "series") ? "tv_results" : "movie_results");
				}
				else
				{
					propertyName = "results";
				}
				if (jsonDocument.RootElement.TryGetProperty(propertyName, out var value3) && value3.ValueKind == JsonValueKind.Array)
				{
					foreach (JsonElement item in value3.EnumerateArray().Take(20))
					{
						list.Add(MapTmdbItem(item, kind, imdbMatch.Success ? imdbMatch.Value.ToLowerInvariant() : null));
					}
				}
			}
			return Ok(list);
		}
		catch (HttpRequestException)
		{
			return StatusCode(502, new
			{
				error = "TMDb indisponibil."
			});
		}
		catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return StatusCode(504, new
			{
				error = "TMDb timeout."
			});
		}
	}
}
