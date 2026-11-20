using System.Text.Json;
using Jellyfin.Plugin.RequestMovie.Models;
using MediaBrowser.Common.Configuration;
namespace Jellyfin.Plugin.RequestMovie.Services;
public sealed class RequestStore {
 private readonly object gate=new();
 private readonly string path;
 private List<MediaRequest> data;
 public RequestStore(IApplicationPaths paths) {
  var dir=Path.Combine(paths.PluginConfigurationsPath,"RequestMovie");
  Directory.CreateDirectory(dir);
  path=Path.Combine(dir,"requests.json");
  try {data=System.IO.File.Exists(path)?JsonSerializer.Deserialize<List<MediaRequest>>(System.IO.File.ReadAllText(path))??[]:[];}
  catch(JsonException){data=[];}
 }
 private void Save() {
  var tmp=path+".tmp";
  System.IO.File.WriteAllText(tmp,JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));
  System.IO.File.Move(tmp,path,true);
 }
 public IReadOnlyList<MediaRequest> List(){lock(gate)return data.Select(Clone).ToArray();}
 private static MediaRequest Clone(MediaRequest r)=>new() {
  Id=r.Id,Title=r.Title,Kind=r.Kind,Year=r.Year,TmdbId=r.TmdbId,ImdbId=r.ImdbId,
  Poster=r.Poster,Overview=r.Overview,RequestedBy=r.RequestedBy,CreatedAt=r.CreatedAt,Status=r.Status,JellyfinItemId=r.JellyfinItemId
 };
 public MediaRequest? AddIfAbsent(NewRequest r,string username,Func<IReadOnlyList<MediaRequest>,bool> duplicate) {
  lock(gate){
   if(duplicate(data))return null;
   var item=new MediaRequest{Title=r.Title,Kind=r.Kind,Year=r.Year,TmdbId=r.TmdbId,ImdbId=r.ImdbId,
    Poster=r.Poster,Overview=r.Overview,RequestedBy=username};
   data.Add(item);Save();return Clone(item);
  }
 }
 public bool Delete(Guid id){lock(gate){var count=data.RemoveAll(x=>x.Id==id);if(count==0)return false;Save();return true;}}
 public bool SetStatus(Guid id,string status){lock(gate){var item=data.FirstOrDefault(x=>x.Id==id);if(item is null)return false;item.Status=status;Save();return true;}}
 public void MarkAvailable(IReadOnlyDictionary<Guid,Guid> found) {
  lock(gate){
   var changed=false;
   foreach(var item in data)if(found.TryGetValue(item.Id,out var id) && (item.Status!="available"||item.JellyfinItemId!=id)){
    item.Status="available";item.JellyfinItemId=id;changed=true;
   }
   if(changed)Save();
  }
 }
}