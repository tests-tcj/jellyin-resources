using System;
using System.Net.Http;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.RequestMovie.Services;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
	public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
	{
		services.AddSingleton<IStartupFilter, FrontendInjection>();
		services.AddSingleton<RequestStore>();
		services.AddSingleton<LibraryChecks>();
		services.AddSingleton<TvRequestLibrarySync>();
		services.AddSingleton<SearchRateLimiter>();
		services.AddHostedService<RequestReconciliationService>();
		services.AddHttpClient("RequestMovie.Tmdb", (HttpClient client) =>
		{
			client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
			client.Timeout = TimeSpan.FromSeconds(12L);
		});
	}
}
