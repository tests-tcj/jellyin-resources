namespace Jellyfin.Plugin.RequestMovie.Models;

public sealed record NewRequest(string Title, string Kind, int? Year, string? TmdbId, string? ImdbId = null, string? Poster = null);
