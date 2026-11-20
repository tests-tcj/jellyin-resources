using System.Text;
using System.Xml;
using Jellyfin.Plugin.RequestMovie.Models;
namespace Jellyfin.Plugin.RequestMovie.Services;
/// <summary>
/// Creates optional Jellyfin TV-visible placeholder entries for pending movie requests.
/// It only modifies filenames owned by this plugin, never unrelated library content.
/// </summary>
public sealed class TvRequestLibrarySync {
 private const string Prefix="RequestMovie_";
 public void Synchronize(IReadOnlyCollection<MediaRequest> requests){
  var settings=Plugin.Instance?.Configuration;
  if(settings?.EnableTvRequestLibrary!=true)return;
  var folder=settings.TvRequestLibraryPath?.Trim();
  if(string.IsNullOrWhiteSpace(folder)||!Path.IsPathFullyQualified(folder)||!Directory.Exists(folder))return;
  var wanted=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var r in requests) {
   if(r.Kind!="movie"||r.Status is not ("pending" or "approved"))continue;
   var stem=Prefix+r.Id.ToString("N");
   wanted.Add(stem);
   var disc=Path.Combine(folder,stem+".disc");
   var nfo=Path.Combine(folder,stem+".nfo");
   try {
    // The disc file is a non-playable placeholder; NFO provides the poster/title.
    if(!File.Exists(disc))File.WriteAllText(disc,"");
    var xml=new XmlWriterSettings{Indent=true,Encoding=new UTF8Encoding(false),OmitXmlDeclaration=false};
    using var writer=XmlWriter.Create(nfo,xml);
    writer.WriteStartElement("movie");
    writer.WriteElementString("title",r.Title);
    writer.WriteElementString("originaltitle",r.Title);
    if(r.Year.HasValue)writer.WriteElementString("year",r.Year.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    if(!string.IsNullOrWhiteSpace(r.Overview))writer.WriteElementString("plot",r.Overview);
    if(!string.IsNullOrWhiteSpace(r.ImdbId))writer.WriteElementString("imdbid",r.ImdbId);
    if(!string.IsNullOrWhiteSpace(r.TmdbId))writer.WriteElementString("tmdbid",r.TmdbId);
    writer.WriteElementString("tag","Cerere film");
    writer.WriteElementString("genre","Cereri");
    writer.WriteEndElement();
   }catch(IOException){}catch(UnauthorizedAccessException){}
  }
  foreach(var disc in Directory.EnumerateFiles(folder,Prefix+"*.disc",SearchOption.TopDirectoryOnly)){
   var stem=Path.GetFileNameWithoutExtension(disc);
   if(!stem.StartsWith(Prefix,StringComparison.Ordinal)||!Guid.TryParseExact(stem[Prefix.Length..],"N",out _))continue;
   if(wanted.Contains(stem))continue;
   try{
    File.Delete(disc);
    var nfo=Path.Combine(folder,stem+".nfo");
    if(File.Exists(nfo))File.Delete(nfo);
   }catch(IOException){}catch(UnauthorizedAccessException){}
  }
 }
}