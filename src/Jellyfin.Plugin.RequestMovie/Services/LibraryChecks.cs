using System.Globalization;
using System.Text;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.RequestMovie.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
namespace Jellyfin.Plugin.RequestMovie.Services;
public sealed class LibraryChecks(ILibraryManager library){
 public static string Normalize(string value) {
  var input=(value??"").Normalize(NormalizationForm.FormD);
  var result=new StringBuilder();
  foreach(var ch in input)if(CharUnicodeInfo.GetUnicodeCategory(ch)!=UnicodeCategory.NonSpacingMark&&char.IsLetterOrDigit(ch))result.Append(char.ToLowerInvariant(ch));
  return result.ToString();
 }
 private static bool SameId(string? a,string? b)=>!string.IsNullOrWhiteSpace(a)&&!string.IsNullOrWhiteSpace(b)&&string.Equals(a.Trim(),b.Trim(),StringComparison.OrdinalIgnoreCase);
 private static bool DifferentId(string? a,string? b)=>!string.IsNullOrWhiteSpace(a)&&!string.IsNullOrWhiteSpace(b)&&!SameId(a,b);
 public static bool Same(NewRequest n,MediaRequest r) {
  if(n.Kind!=r.Kind)return false;
  if(SameId(n.TmdbId,r.TmdbId)||SameId(n.ImdbId,r.ImdbId))return true;
  if(DifferentId(n.TmdbId,r.TmdbId)||DifferentId(n.ImdbId,r.ImdbId))return false;
  return Normalize(n.Title)==Normalize(r.Title)&&(!n.Year.HasValue||!r.Year.HasValue||n.Year==r.Year);
 }
 private static bool Matches(NewRequest n,BaseItem item) {
  var tmdb=item.GetProviderId(MetadataProvider.Tmdb);
  var imdb=item.GetProviderId(MetadataProvider.Imdb);
  if(SameId(n.TmdbId,tmdb)||SameId(n.ImdbId,imdb))return true;
  if(DifferentId(n.TmdbId,tmdb)||DifferentId(n.ImdbId,imdb))return false;
  return Normalize(n.Title)==Normalize(item.Name)&&(!n.Year.HasValue||!item.ProductionYear.HasValue||n.Year==item.ProductionYear);
 }
 public BaseItem? Match(NewRequest n){
  var items=library.GetItemsResult(new InternalItemsQuery {
   IncludeItemTypes=[n.Kind=="series"?BaseItemKind.Series:BaseItemKind.Movie],Recursive=true,
   IsVirtualItem=false,IsPlaceHolder=false,EnableTotalRecordCount=false
  });
  return items.Items.FirstOrDefault(x=>Matches(n,x));
 }
 public bool Exists(NewRequest n)=>Match(n)!=null;
 public IReadOnlyDictionary<Guid,Guid> FindAvailable(IReadOnlyCollection<MediaRequest> requests){
  var pending=requests.Where(x=>x.Status is "pending" or "approved" or "available").ToArray();
  var found=new Dictionary<Guid,Guid>();
  if(pending.Length==0)return found;
  var kinds=pending.Select(x=>x.Kind=="series"?BaseItemKind.Series:BaseItemKind.Movie).Distinct().ToArray();
  var libraryItems=library.GetItemsResult(new InternalItemsQuery {
   IncludeItemTypes=kinds,Recursive=true,IsVirtualItem=false,IsPlaceHolder=false,EnableTotalRecordCount=false
  });
  foreach(var media in libraryItems.Items) {
   var kind=media is MediaBrowser.Controller.Entities.TV.Series?"series":"movie";
   foreach(var r in pending) {
    if(r.Kind!=kind||found.ContainsKey(r.Id))continue;
    if(Matches(new NewRequest(r.Title,r.Kind,r.Year,r.TmdbId,r.ImdbId),media))found[r.Id]=media.Id;
   }
  }
  return found;
 }
}