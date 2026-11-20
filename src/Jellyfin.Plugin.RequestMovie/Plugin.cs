using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
namespace Jellyfin.Plugin.RequestMovie;
public sealed class Plugin : BasePlugin<Configuration.PluginConfiguration>, IHasWebPages {
 public static readonly Guid PluginId=new("f8517621-7c60-4e19-9696-67f9b31fa092");
 public static Plugin? Instance {get;private set;}
 public Plugin(IApplicationPaths paths,IXmlSerializer serializer):base(paths,serializer){Instance=this;}
 public override string Name=>"Request Movie";
 public override Guid Id=>PluginId;
 public IEnumerable<PluginPageInfo> GetPages()=>[new PluginPageInfo {
 Name=Name,EmbeddedResourcePath="Jellyfin.Plugin.RequestMovie.Configuration.configPage.html"
 }];
}