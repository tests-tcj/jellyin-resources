using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Trailer.Controllers;

[ApiController]
[Authorize]
[Route("NfoTrailer")]
public sealed class TrailerController(ILibraryManager libraryManager) : ControllerBase
{
	[AllowAnonymous]
	[HttpGet("Script.js")]
	public IActionResult Script()
	{
		using Stream stream = typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.Trailer.Client.trailer.js");
		if (stream == null)
		{
			return NotFound();
		}
		using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
		Response.Headers.CacheControl = "no-store";
		return Content(streamReader.ReadToEnd(), "text/javascript", Encoding.UTF8);
	}

	[HttpGet("Movie/{id:guid}")]
	public IActionResult MovieTrailer(Guid id)
	{
		BaseItem itemById = libraryManager.GetItemById(id);
		Movie val = (Movie)(object)((itemById is Movie) ? itemById : null);
		if (val == null || string.IsNullOrWhiteSpace(((BaseItem)val).Path))
		{
			return NotFound();
		}
		string directoryName = Path.GetDirectoryName(((BaseItem)val).Path);
		if (string.IsNullOrWhiteSpace(directoryName))
		{
			return Ok(new
			{
				videoId = (string)null
			});
		}
		IEnumerable<string> enumerable = new string[3]
		{
			Path.ChangeExtension(((BaseItem)val).Path, ".nfo"),
			Path.Combine(directoryName, "movie.nfo"),
			Path.Combine(directoryName, Path.GetFileName(directoryName) + ".nfo")
		}.Distinct(StringComparer.OrdinalIgnoreCase);
		bool flag = false;
		foreach (string item in enumerable)
		{
			try
			{
				if (!System.IO.File.Exists(item))
				{
					continue;
				}
				flag = true;
				using XmlReader reader = XmlReader.Create(item, new XmlReaderSettings
				{
					DtdProcessing = DtdProcessing.Prohibit,
					XmlResolver = null,
					MaxCharactersInDocument = 2000000L
				});
				string text = XDocument.Load(reader).Root?.Element("trailer")?.Value?.Trim();
				if (!string.IsNullOrWhiteSpace(text))
				{
					Match match = Regex.Match(text, "(?:video_id=|youtu\\.be/|youtube(?:-nocookie)?\\.com/(?:watch\\?(?:[^#]*?&)?v=|embed/|shorts/))([A-Za-z0-9_-]{11})(?![A-Za-z0-9_-])", RegexOptions.IgnoreCase);
					if (match.Success)
					{
						return Ok(new
						{
							videoId = match.Groups[1].Value
						});
					}
				}
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
			catch (XmlException)
			{
			}
		}
		return Ok(new
		{
			videoId = (string)null,
			reason = (flag ? "no-valid-youtube-trailer" : "nfo-not-found")
		});
	}
}
