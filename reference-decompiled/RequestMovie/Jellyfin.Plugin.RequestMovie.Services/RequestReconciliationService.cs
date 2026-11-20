using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RequestMovie.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RequestMovie.Services;

public sealed class RequestReconciliationService(RequestStore store, LibraryChecks library, TvRequestLibrarySync tvLibrary, ILogger<RequestReconciliationService> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			await Task.Delay(TimeSpan.FromMinutes(2L), stoppingToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMinutes(15L));
		do
		{
			try
			{
				IReadOnlyList<MediaRequest> requests = store.List();
				IReadOnlyDictionary<Guid, Guid> identifiers = library.FindAvailable(requests);
				int num = store.MarkAvailable(identifiers);
				if (num != 0)
				{
					logger.LogInformation("Request Movie: marked {Count} requests as available", num);
				}
				tvLibrary.Sync(store.List());
			}
			catch (Exception exception)
			{
				logger.LogWarning(exception, "Request Movie: library reconciliation failed");
			}
		}
		while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(continueOnCapturedContext: false));
	}
}
