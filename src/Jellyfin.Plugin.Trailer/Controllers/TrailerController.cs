using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities.Movies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Jellyfin.Plugin.Trailer.Controllers;
[ApiController]
[Authorize]
[Route("NfoTrailer")]
public sealed class TrailerController(ILibraryManager libraryManager) : ControllerBase
{
 [AllowAnonymous]
 [HttpGet("Script.js")]
 public IActionResult Script() {
  using var stream=typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.Trailer.Client.trailer.js");
  if(stream is null)return NotFound();
  using var reader=new StreamReader(stream,Encoding.UTF8);
  Response.Headers.CacheControl="no-store";
  return Content(reader.ReadToEnd(),"text/javascript",Encoding.UTF8);
 }
 [HttpGet("Movie/{id:guid}")]
 public IActionResult MovieTrailer(Guid id) {
  var item=libraryManager.GetItemById(id);
  if(item is not Movie movie || string.IsNullOrWhiteSpace(movie.Path))return NotFound();
  var folder=Path.GetDirectoryName(movie.Path);
  if(string.IsNullOrEmpty(folder))return Ok(new {videoId=(string?)null,reason="no-movie-directory"});
  var candidates=new [] {Path.ChangeExtension(movie.Path,".nfo"),Path.Combine(folder,"movie.nfo"),Path.Combine(folder,Path.GetFileName(folder)+".nfo")}.Distinct(StringComparer.OrdinalIgnoreCase);
  var found=false;
  foreach(var path in candidates) {
   try {
    if(!System.IO.File.Exists(path))continue;
    found=true;
    using var reader=XmlReader.Create(path,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2_000_000});
    var xml=XDocument.Load(reader);
    var trailer=xml.Root?.Element("trailer")?.Value?.Trim()??"";
    var m=Regex.Match(trailer,@"(?:video_id=|youtu\.be/|youtube(?:-nocookie)?\.com/(?:watch\?(?:[^#]*?&)?v=|embed/|shorts/))([A-Za-z0-9_-]{11})(?![A-Za-z0-9_-])",RegexOptions.IgnoreCase);
    if(m.Success)return Ok(new {videoId=m.Groups[1].Value});
   } catch(IOException) {} catch(UnauthorizedAccessException) {} catch(XmlException) {}
  }
  return Ok(new {videoId=(string?)null,reason=found?"no-valid-youtube-trailer":"nfo-not-found"});
 }
}