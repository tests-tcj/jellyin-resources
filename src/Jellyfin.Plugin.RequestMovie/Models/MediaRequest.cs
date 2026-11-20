using System;

namespace Jellyfin.Plugin.RequestMovie.Models;

public sealed class MediaRequest
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public string Title { get; set; } = string.Empty;

	public string Kind { get; set; } = "movie";

	public int? Year { get; set; }

	public string? TmdbId { get; set; }

	public string? ImdbId { get; set; }

	public string? Poster { get; set; }

	public Guid? JellyfinItemId { get; set; }

	public string RequestedBy { get; set; } = string.Empty;

	public string Status { get; set; } = "pending";

	public DateTime? AvailableUtc { get; set; }

	public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

	public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public record StatusChange(string Status);
