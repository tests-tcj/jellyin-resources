using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Trailer;

public sealed class Plugin : BasePlugin<BasePluginConfiguration>
{
	public static readonly Guid PluginId = new Guid("e30f17c1-cd72-4bdc-8225-38d122947719");

	public override string Name => "NFO Trailer";

	public override Guid Id => PluginId;

	public override string Description => "Plays YouTube trailers stored in movie NFO files inside Jellyfin movie details.";

	public Plugin(IApplicationPaths paths, IXmlSerializer serializer)
		: base(paths, serializer)
	{
	}
}
