using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
namespace Jellyfin.Plugin.RequestMovie.Services;
public sealed class PluginServiceRegistrator:IPluginServiceRegistrator {
 public void RegisterServices(IServiceCollection services,IServerApplicationHost host) {
  services.AddSingleton<IStartupFilter,FrontendInjection>();
  services.AddSingleton<RequestStore>();
  services.AddSingleton<LibraryChecks>();
  services.AddSingleton<SearchRateLimiter>();
  services.AddSingleton<TvRequestLibrarySync>();
  services.AddHostedService<RequestReconciliationService>();
  services.AddHttpClient("RequestMovie.Tmdb",c=>{c.BaseAddress=new Uri("https://api.themoviedb.org/3/");c.Timeout=TimeSpan.FromSeconds(12);});
 }
}