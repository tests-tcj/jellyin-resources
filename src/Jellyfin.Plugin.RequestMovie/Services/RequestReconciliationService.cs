using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Jellyfin.Plugin.RequestMovie.Services;
public sealed class RequestReconciliationService(RequestStore store,LibraryChecks library,TvRequestLibrarySync tv,ILogger<RequestReconciliationService> logger):BackgroundService {
 protected override async Task ExecuteAsync(CancellationToken stoppingToken){
  while(!stoppingToken.IsCancellationRequested){
   try {
    var requests=store.List();
    store.MarkAvailable(library.FindAvailable(requests));
    tv.Sync(store.List());
   }catch(Exception ex){logger.LogWarning(ex,"RequestMovie reconciliation failed");}
   try{await Task.Delay(TimeSpan.FromMinutes(10),stoppingToken);}catch(OperationCanceledException){break;}
  }
 }
}