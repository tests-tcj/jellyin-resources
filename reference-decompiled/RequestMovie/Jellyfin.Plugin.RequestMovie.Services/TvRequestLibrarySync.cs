using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.RequestMovie.Configuration;
using Jellyfin.Plugin.RequestMovie.Models;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RequestMovie.Services;

public sealed class TvRequestLibrarySync(ILogger<TvRequestLibrarySync> logger)
{
	private const string MarkerFile = ".request-movie-managed";

	private static readonly Regex ManagedDirectory = new Regex("^RequestMovie-([0-9a-f]{32})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public void Sync(IReadOnlyCollection<MediaRequest> all)
	{
		PluginConfiguration pluginConfiguration = ((BasePlugin<PluginConfiguration>)(object)Plugin.Instance)?.Configuration;
		if (pluginConfiguration == null || !pluginConfiguration.EnableTvRequestLibrary || string.IsNullOrWhiteSpace(pluginConfiguration.TvRequestLibraryPath))
		{
			return;
		}
		string text = pluginConfiguration.TvRequestLibraryPath.Trim();
		if (!Path.IsPathFullyQualified(text) || !Directory.Exists(text))
		{
			logger.LogWarning("Request Movie: TV folder must be an existing absolute directory: {Path}", text);
			return;
		}
		string fullPath = Path.GetFullPath(text);
		Dictionary<Guid, MediaRequest> dictionary = all.Where((MediaRequest r) =>
		{
			if (r.Kind == "movie")
			{
				string status = r.Status;
				return status == "pending" || status == "approved";
			}
			return false;
		}).ToDictionary((MediaRequest r) => r.Id, (MediaRequest r) => r);
		foreach (MediaRequest value in dictionary.Values)
		{
			string text2 = Path.Combine(fullPath, "RequestMovie-" + value.Id.ToString("N"));
			try
			{
				if (Directory.Exists(text2) && !File.Exists(Path.Combine(text2, ".request-movie-managed")))
				{
					logger.LogWarning("Request Movie: skipping unmanaged directory {Path}", text2);
					continue;
				}
				Directory.CreateDirectory(text2);
				File.WriteAllText(Path.Combine(text2, ".request-movie-managed"), value.Id.ToString("D"), Encoding.UTF8);
				string text3 = Regex.Replace(value.Title.Trim(), "[<>:\"/\\\\|?*\\x00-\\x1F]", " ").Trim();
				if (text3.Length == 0)
				{
					text3 = "Untitled";
				}
				if (text3.Length > 110)
				{
					text3 = text3.Substring(0, 110).Trim();
				}
				string text4 = text3 + (value.Year.HasValue ? (" (" + value.Year.Value + ")") : "");
				string text5 = Path.Combine(text2, text4 + ".disc");
				foreach (string item in Directory.EnumerateFiles(text2, "*.disc"))
				{
					if (!string.Equals(item, text5, StringComparison.Ordinal))
					{
						File.Delete(item);
					}
				}
				if (!File.Exists(text5))
				{
					File.WriteAllText(text5, string.Empty);
				}
				string text6 = SecurityElement.Escape(value.Title) ?? "";
				string text7 = ((value.Status == "approved") ? "Aprobată" : "În așteptare");
				string text8 = SecurityElement.Escape("Cerere Jellyfin - " + text7 + ". Solicitat de " + value.RequestedBy + ". Filmul nu este încă disponibil.") ?? "";
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
				stringBuilder.AppendLine("<movie><title>" + text6 + "</title>");
				if (value.Year.HasValue)
				{
					stringBuilder.AppendLine("<year>" + value.Year.Value + "</year>");
				}
				if (!string.IsNullOrWhiteSpace(value.TmdbId))
				{
					stringBuilder.AppendLine("<tmdbid>" + SecurityElement.Escape(value.TmdbId) + "</tmdbid>");
				}
				if (!string.IsNullOrWhiteSpace(value.ImdbId))
				{
					stringBuilder.AppendLine("<imdbid>" + SecurityElement.Escape(value.ImdbId) + "</imdbid>");
				}
				stringBuilder.AppendLine("<plot>" + text8 + "</plot><tag>Request Movie</tag></movie>");
				File.WriteAllText(Path.Combine(text2, "movie.nfo"), stringBuilder.ToString(), Encoding.UTF8);
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
			{
				logger.LogWarning(ex, "Request Movie: could not create TV placeholder {RequestId}", value.Id);
			}
		}
		foreach (string item2 in Directory.EnumerateDirectories(fullPath, "RequestMovie-*", SearchOption.TopDirectoryOnly))
		{
			string fileName = Path.GetFileName(item2);
			Match match = ManagedDirectory.Match(fileName);
			if (!match.Success || !Guid.TryParseExact(match.Groups[1].Value, "N", out var result) || dictionary.ContainsKey(result))
			{
				continue;
			}
			string path = Path.Combine(item2, ".request-movie-managed");
			if (!File.Exists(path))
			{
				continue;
			}
			try
			{
				if (!Guid.TryParse(File.ReadAllText(path).Trim(), out var result2) || result2 != result || Directory.EnumerateDirectories(item2).Any())
				{
					continue;
				}
				string[] array = Directory.EnumerateFiles(item2).ToArray();
				if (!array.Any((string p) => Path.GetFileName(p) != ".request-movie-managed" && Path.GetFileName(p) != "movie.nfo" && !p.EndsWith(".disc", StringComparison.OrdinalIgnoreCase)))
				{
					string[] array2 = array;
					for (int num = 0; num < array2.Length; num++)
					{
						File.Delete(array2[num]);
					}
					Directory.Delete(item2);
				}
			}
			catch (Exception ex2) when (ex2 is IOException || ex2 is UnauthorizedAccessException)
			{
				logger.LogWarning(ex2, "Request Movie: could not retire TV placeholder {RequestId}", result);
			}
		}
	}
}
