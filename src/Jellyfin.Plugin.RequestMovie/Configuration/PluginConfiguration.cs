using MediaBrowser.Model.Plugins;
namespace Jellyfin.Plugin.RequestMovie.Configuration;
public sealed class PluginConfiguration : BasePluginConfiguration {
 public string TmdbApiKey {get;set;}="";
 public bool AllowRequests {get;set;}=true;
 public int SearchesPerUserPerHour {get;set;}=30;
 public bool EnableTvRequestLibrary {get;set;}
 public string TvRequestLibraryPath {get;set;}="";
 public string AdminActionButtonLabel {get;set;}="Deschide";
 public string AdminActionUrlTemplate {get;set;}="";
}