namespace Jellyfin.Plugin.RequestMovie.Models;
public record NewRequest(string Title,string Kind,int? Year=null,string? TmdbId=null,string? ImdbId=null,string? Poster=null,string? Overview=null);
public record StatusChange(string Status);
public sealed class MediaRequest {
 public Guid Id {get;set;}=Guid.NewGuid();
 public string Title {get;set;}="";
 public string Kind {get;set;}="movie";
 public int? Year {get;set;}
 public string? TmdbId {get;set;}
 public string? ImdbId {get;set;}
 public string? Poster {get;set;}
 public string? Overview {get;set;}
 public string RequestedBy {get;set;}="";
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
 public string Status {get;set;}="pending";
 public Guid? JellyfinItemId {get;set;}
}