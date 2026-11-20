namespace Jellyfin.Plugin.RequestMovie.Services;
public sealed class SearchRateLimiter {
 private readonly object gate=new();
 private readonly Dictionary<string,List<DateTime>> searches=new();
 public (bool Allowed,int RetryAfterSeconds) TryAcquire(string user,int maxPerHour){
  lock(gate){
   var now=DateTime.UtcNow;
   if(!searches.TryGetValue(user,out var list)){list=[];searches[user]=list;}
   list.RemoveAll(t=>t<=now.AddHours(-1));
   if(list.Count>=maxPerHour)return (false,Math.Max(1,(int)Math.Ceiling((list[0].AddHours(1)-now).TotalSeconds)));
   list.Add(now);return(true,0);
  }
 }
}