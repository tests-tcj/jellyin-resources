using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
namespace Jellyfin.Plugin.Trailer.Services;
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
 public void RegisterServices(IServiceCollection services, IServerApplicationHost host)
 { services.AddSingleton<IStartupFilter, FrontendInjection>(); }
}