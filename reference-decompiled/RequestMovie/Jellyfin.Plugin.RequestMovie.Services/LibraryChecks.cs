using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.RequestMovie.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Plugin.RequestMovie.Services;

public sealed class LibraryChecks(ILibraryManager library)
{
	public static string Normalize(string text)
	{
		string text2 = (text ?? "").Normalize(NormalizationForm.FormD);
		StringBuilder stringBuilder = new StringBuilder();
		string text3 = text2;
		foreach (char c in text3)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(char.ToLowerInvariant(c));
			}
		}
		return stringBuilder.ToString();
	}

	public bool Exists(NewRequest request)
	{
		return Match(request) != null;
	}

	public bool Exists(MediaRequest request)
	{
		return Match(new NewRequest(request.Title, request.Kind, request.Year, request.TmdbId, request.ImdbId)) != null;
	}

	public static bool Same(NewRequest request, MediaRequest existing)
	{
		if (existing.Kind != request.Kind)
		{
			return false;
		}
		if (SameId(request.TmdbId, existing.TmdbId) || SameId(request.ImdbId, existing.ImdbId))
		{
			return true;
		}
		if (Normalize(request.Title) != Normalize(existing.Title))
		{
			return false;
		}
		if (DifferentIds(request.TmdbId, existing.TmdbId) || DifferentIds(request.ImdbId, existing.ImdbId))
		{
			return false;
		}
		if (request.Year.HasValue && existing.Year.HasValue)
		{
			return request.Year == existing.Year;
		}
		return true;
	}

	private static bool SameId(string? a, string? b)
	{
		if (!string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b))
		{
			return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static bool DifferentIds(string? a, string? b)
	{
		if (!string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b))
		{
			return !SameId(a, b);
		}
		return false;
	}

	private static bool Matches(NewRequest request, BaseItem item)
	{
		string providerId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)item, (MetadataProvider)3);
		string providerId2 = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)item, (MetadataProvider)2);
		if (SameId(request.TmdbId, providerId) || SameId(request.ImdbId, providerId2))
		{
			return true;
		}
		if (DifferentIds(request.TmdbId, providerId) || DifferentIds(request.ImdbId, providerId2))
		{
			return false;
		}
		if (Normalize(request.Title) != Normalize(item.Name))
		{
			return false;
		}
		if (request.Year.HasValue && item.ProductionYear.HasValue)
		{
			return request.Year == item.ProductionYear;
		}
		return true;
	}

	public BaseItem? Match(NewRequest request)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected Obj, but got Unknown
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected I4, but got Unknown
		BaseItemKind val = (BaseItemKind)((request.Kind == "series") ? 28 : 13);
		ILibraryManager val2 = library;
		InternalItemsQuery val3 = new InternalItemsQuery();
		val3.IncludeItemTypes = new BaseItemKind[1] { (BaseItemKind)(int)val };
		val3.Recursive = true;
		val3.IsVirtualItem = false;
		val3.IsPlaceHolder = false;
		val3.EnableTotalRecordCount = false;
		return val2.GetItemsResult(val3).Items.FirstOrDefault((BaseItem item) => Matches(request, item));
	}

	public IReadOnlyDictionary<Guid, Guid> FindAvailable(IReadOnlyCollection<MediaRequest> requests)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Expected Obj, but got Unknown
		MediaRequest[] array = requests.Where((MediaRequest r) =>
		{
			string status = r.Status;
			return status == "pending" || status == "approved" || status == "available";
		}).ToArray();
		if (array.Length == 0)
		{
			return new Dictionary<Guid, Guid>();
		}
		BaseItemKind[] includeItemTypes = array.Select((MediaRequest r) => (r.Kind == "series") ? ((BaseItemKind)28) : ((BaseItemKind)13)).Distinct().ToArray();
		QueryResult<BaseItem> itemsResult = library.GetItemsResult(new InternalItemsQuery
		{
			IncludeItemTypes = includeItemTypes,
			Recursive = true,
			IsVirtualItem = false,
			IsPlaceHolder = false,
			EnableTotalRecordCount = false
		});
		Dictionary<Guid, Guid> dictionary = new Dictionary<Guid, Guid>();
		foreach (BaseItem item in itemsResult.Items)
		{
			string text = ((item is Series) ? "series" : "movie");
			MediaRequest[] array2 = array;
			foreach (MediaRequest mediaRequest in array2)
			{
				if (!(mediaRequest.Kind != text) && !dictionary.ContainsKey(mediaRequest.Id) && Matches(new NewRequest(mediaRequest.Title, mediaRequest.Kind, mediaRequest.Year, mediaRequest.TmdbId, mediaRequest.ImdbId), item))
				{
					dictionary[mediaRequest.Id] = item.Id;
				}
			}
			if (dictionary.Count == array.Length)
			{
				break;
			}
		}
		return dictionary;
	}
}
