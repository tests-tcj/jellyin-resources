using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.RequestMovie.Models;
using Jellyfin.Plugin.RequestMovie.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Jellyfin.Plugin.RequestMovie.Controllers;
[ApiController]
[Route("RequestMovie")]
[Authorize]
public sealed class RequestMovieController(RequestStore store,LibraryChecks library,SearchRateLimiter limiter,IHttpClientFactory factory):ControllerBase {
 private string Username=>User.Identity?.Name??User.FindFirst("name")?.Value??User.FindFirst("JellyfinUserId")?.Value??"Jellyfin user";
 private static string? Invalid(NewRequest request){
  if(string.IsNullOrWhiteSpace(request.Title)||request.Title.Length is <2 or >160)return "Titlul trebuie sa aiba 2-160 caractere.";
  if(request.Kind is not ("movie" or "series"))return "Tip invalid.";
  if(request.Year.HasValue&&(request.Year<1880||request.Year>2100))return "An invalid.";
  if(!string.IsNullOrEmpty(request.TmdbId)&&!Regex.IsMatch(request.TmdbId,@"^\d{1,20}$"))return "TMDb ID invalid.";
  if(!string.IsNullOrEmpty(request.ImdbId)&&!Regex.IsMatch(request.ImdbId,@"^tt\d{7,12}$",RegexOptions.IgnoreCase))return "IMDb ID invalid.";
  if(!string.IsNullOrEmpty(request.Poster)&&(!request.Poster.StartsWith("https://image.tmdb.org/t/p/",StringComparison.Ordinal)||request.Poster.Length>300))return "Poster invalid.";
  return null;
 }
 private async Task<string?> Imdb(string kind,string? tmdbId,CancellationToken ct){
  var key=Plugin.Instance?.Configuration.TmdbApiKey;
  if(string.IsNullOrWhiteSpace(tmdbId)||string.IsNullOrWhiteSpace(key))return null;
  try {
   using var resp=await factory.CreateClient("RequestMovie.Tmdb").GetAsync($"{(kind=="series"?"tv":"movie")}/{tmdbId}/external_ids?api_key={Uri.EscapeDataString(key)}",ct);
   if(!resp.IsSuccessStatusCode)return null;
   using var doc=await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken:ct);
   return doc?.RootElement.TryGetProperty("imdb_id",out var id)==true?id.GetString():null;
  }catch(HttpRequestException){return null;}catch(TaskCanceledException)when(!ct.IsCancellationRequested){return null;}
 }
 [AllowAnonymous][HttpGet("Nav.js")]
 public IActionResult Nav(){
  using var s=typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.RequestMovie.Client.requestMovieNav.js");
  if(s is null)return NotFound();
  using var r=new StreamReader(s,Encoding.UTF8);Response.Headers.CacheControl="no-store";return Content(r.ReadToEnd(),"text/javascript",Encoding.UTF8);
 }
 [HttpGet("Ui")]
 public IActionResult Ui(){
  using var s=typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.RequestMovie.Client.requestMovieUi.html");
  if(s is null)return NotFound();
  using var r=new StreamReader(s,Encoding.UTF8);Response.Headers.CacheControl="no-store";return Content(r.ReadToEnd(),"text/html",Encoding.UTF8);
 }
 [HttpGet("Requests")]
 public IActionResult Requests(){
  var list=store.List();store.MarkAvailable(library.FindAvailable(list));return Ok(store.List());
 }
 [HttpGet("Check")]
 public async Task<IActionResult> Check([FromQuery]string title,[FromQuery]string kind="movie",[FromQuery]int? year=null,[FromQuery]string? tmdbId=null,[FromQuery]string? imdbId=null){
  var n=new NewRequest(title??"",kind,year,tmdbId,imdbId);if(Invalid(n) is { } error)return BadRequest(new{error});
  if(string.IsNullOrWhiteSpace(n.ImdbId))n=n with {ImdbId=await Imdb(kind,n.TmdbId,HttpContext.RequestAborted)};
  var item=library.Match(n);
  if(item!=null)return Ok(new{state="available",jellyfinItemId=item.Id.ToString("N")});
  return Ok(new{state=store.List().Any(x=>x.Status is "pending" or "approved" or "available"&&LibraryChecks.Same(n,x))?"requested":"new"});
 }
 [HttpPost("Requests")]
 public async Task<IActionResult> Create([FromBody]NewRequest n){
  if(Plugin.Instance?.Configuration.AllowRequests!=true)return BadRequest(new{error="Cererile sunt dezactivate."});
  if(Invalid(n) is { } error)return BadRequest(new{error});
  if(string.IsNullOrWhiteSpace(n.ImdbId))n=n with{ImdbId=await Imdb(n.Kind,n.TmdbId,HttpContext.RequestAborted)};
  if(library.Exists(n))return Conflict(new{state="available"});
  var result=store.AddIfAbsent(n,Username,all=>all.Any(x=>x.Status is "pending" or "approved" or "available"&&LibraryChecks.Same(n,x)));
  return result==null?Conflict(new{state="requested"}):Ok(result);
 }
 [HttpDelete("Requests/{id:guid}")][Authorize(Policy="RequiresElevation")]
 public IActionResult Delete(Guid id)=>store.Delete(id)?NoContent():NotFound();
 [HttpPatch("Requests/{id:guid}")][Authorize(Policy="RequiresElevation")]
 public IActionResult SetStatus(Guid id,[FromBody]StatusChange input){
  if(input.Status is not ("pending" or "approved" or "rejected" or "available"))return BadRequest(new{error="Status invalid."});
  return store.SetStatus(id,input.Status)?NoContent():NotFound();
 }
 [HttpGet("Admin/Capabilities")][Authorize(Policy="RequiresElevation")]
 public IActionResult Capabilities(){
  var c=Plugin.Instance?.Configuration;var label=c?.AdminActionButtonLabel?.Trim()??"";
  return Ok(new{canDelete=true,actionEnabled=!string.IsNullOrWhiteSpace(c?.AdminActionUrlTemplate),actionLabel=string.IsNullOrWhiteSpace(label)?"Deschide":label[..Math.Min(60,label.Length)]});
 }
 [HttpGet("Admin/Action/{id:guid}/Url")][Authorize(Policy="RequiresElevation")]
 public IActionResult AdminAction(Guid id) {
  var entry=store.List().FirstOrDefault(x=>x.Id==id);
  if(entry==null)return NotFound();
  if(entry.Kind!="movie"||entry.Status!="pending")return BadRequest(new{error="Disponibil doar pentru filme in asteptare."});
  var template=Plugin.Instance?.Configuration.AdminActionUrlTemplate??"";
  if(template.Length>2048||template.IndexOfAny(['\r','\n'])>=0)return BadRequest(new{error="URL invalid."});
  var vars=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
   ["title"]=entry.Title,["year"]=entry.Year?.ToString()??"",["tmdbId"]=entry.TmdbId??"",
   ["imdbId"]=entry.ImdbId??"",["kind"]=entry.Kind,["requestId"]=entry.Id.ToString("D")
  };
  var url=Regex.Replace(template,@"\{([a-zA-Z][a-zA-Z0-9]*)\}",m=>vars.TryGetValue(m.Groups[1].Value,out var value)?Uri.EscapeDataString(value):m.Value);
  if(url.Contains('{')||url.Contains('}')||!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme is not("http" or "https")||!string.IsNullOrEmpty(uri.UserInfo))return BadRequest(new{error="URL invalid."});
  return Ok(new{url=uri.AbsoluteUri});
 }
 private static object Map(JsonElement value,string kind,string? knownImdb=null){
  var titleField=kind=="series"?"name":"title";var dateField=kind=="series"?"first_air_date":"release_date";
  var title=value.TryGetProperty(titleField,out var t)?t.GetString()??"":"";var date=value.TryGetProperty(dateField,out var d)?d.GetString()??"":"";
  var poster=value.TryGetProperty("poster_path",out var p)&&p.ValueKind==JsonValueKind.String?p.GetString():null;
  var overview=value.TryGetProperty("overview",out var o)&&o.ValueKind==JsonValueKind.String?o.GetString():null;
  var imdb=knownImdb??(value.TryGetProperty("imdb_id",out var i)&&i.ValueKind==JsonValueKind.String?i.GetString():null);
  return new{title,kind,year=date.Length>=4&&int.TryParse(date[..4],out var y)?y:(int?)null,tmdbId=value.GetProperty("id").ToString(),imdbId=imdb,poster=string.IsNullOrEmpty(poster)?null:"https://image.tmdb.org/t/p/w185"+poster,overview};
 }
 [HttpGet("Search")]
 public async Task<IActionResult> Search([FromQuery]string query,[FromQuery]string kind="movie",CancellationToken cancellationToken=default){
  query=query?.Trim()??"";if(kind is not ("movie" or "series"))return BadRequest(new{error="Tip invalid."});
  var imdb=Regex.Match(query,@"^tt\d{7,12}$",RegexOptions.IgnoreCase);
  var tmdb=Regex.Match(query,@"^(?:tmdb\s*:\s*)?(\d{1,12})$",RegexOptions.IgnoreCase);
  if(query.Length>120||(!imdb.Success&&!tmdb.Success&&query.Length<2))return BadRequest(new{error="Cautarea trebuie sa aiba 2-120 caractere."});
  var key=Plugin.Instance?.Configuration.TmdbApiKey;
  if(string.IsNullOrWhiteSpace(key))return BadRequest(new{error="Configureaza cheia TMDb in plugin."});
  var user=User.FindFirst("JellyfinUserId")?.Value??Username;
  var max=Math.Clamp(Plugin.Instance?.Configuration.SearchesPerUserPerHour??30,1,200);
  var quota=limiter.TryAcquire(user,max);
  if(!quota.Allowed){Response.Headers["Retry-After"]=quota.RetryAfterSeconds.ToString();return StatusCode(429,new{error=$"Limita {max} cautari/ora.",retryAfterSeconds=quota.RetryAfterSeconds});}
  var media=kind=="series"?"tv":"movie";
  var path=imdb.Success?$"find/{imdb.Value.ToLowerInvariant()}?api_key={Uri.EscapeDataString(key)}&external_source=imdb_id&language=ro-RO":
   tmdb.Success?$"{media}/{tmdb.Groups[1].Value}?api_key={Uri.EscapeDataString(key)}&language=ro-RO":
   $"search/{media}?api_key={Uri.EscapeDataString(key)}&language=ro-RO&query={Uri.EscapeDataString(query)}";
  try {
   using var resp=await factory.CreateClient("RequestMovie.Tmdb").GetAsync(path,cancellationToken);
   if(resp.StatusCode==HttpStatusCode.NotFound&&(imdb.Success||tmdb.Success))return Ok(Array.Empty<object>());
   if(!resp.IsSuccessStatusCode)return StatusCode(502,new{error="TMDb a returnat o eroare."});
   using var doc=await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken:cancellationToken);
   if(doc is null)return Ok(Array.Empty<object>());
   var results=new List<object>();
   if(tmdb.Success)results.Add(Map(doc.RootElement,kind));
   else {
    var keyName=imdb.Success?(kind=="series"?"tv_results":"movie_results"):"results";
    if(doc.RootElement.TryGetProperty(keyName,out var arr)&&arr.ValueKind==JsonValueKind.Array)
     foreach(var item in arr.EnumerateArray().Take(20))results.Add(Map(item,kind,imdb.Success?imdb.Value.ToLowerInvariant():null));
   }
   return Ok(results);
  }catch(HttpRequestException){return StatusCode(502,new{error="TMDb indisponibil."});}
   catch(TaskCanceledException)when(!cancellationToken.IsCancellationRequested){return StatusCode(504,new{error="TMDb timeout."});}
 }
}