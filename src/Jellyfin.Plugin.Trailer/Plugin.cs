using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
namespace Jellyfin.Plugin.Trailer;
public sealed class Plugin : BasePlugin<BasePluginConfiguration>
{
 public static readonly Guid PluginId = new("e30f17c1-cd72-4bdc-8225-38d122947719");
 public Plugin(IApplicationPaths paths, IXmlSerializer xml) : base(paths,xml) { }
 public override Guid Id => PluginId;
 public override string Name => "NFO Trailer";
 public override string Description => "Show YouTube trailers from local NFO files in Jellyfin movie details.";
}