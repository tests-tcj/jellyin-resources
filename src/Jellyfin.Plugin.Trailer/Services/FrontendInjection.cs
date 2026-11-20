using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
namespace Jellyfin.Plugin.Trailer.Services;
public sealed class FrontendInjection : IStartupFilter
{
 public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app => {
  app.Use(async (context, nextMiddleware) => {
   var p=context.Request.Path.Value??"";
   bool web=p.EndsWith("/web/index.html",StringComparison.OrdinalIgnoreCase) || p.EndsWith("/web/",StringComparison.OrdinalIgnoreCase) || p.EndsWith("/web",StringComparison.OrdinalIgnoreCase);
   if (!HttpMethods.IsGet(context.Request.Method)||!web) { await nextMiddleware(context);return; }
   var original=context.Response.Body;
   await using var buffer=new MemoryStream();
   context.Response.Body=buffer;
   try {
    await nextMiddleware(context);
    buffer.Position=0;
    if(context.Response.StatusCode!=200 || context.Response.Headers.ContentEncoding.Count!=0) {
     context.Response.ContentLength=buffer.Length;
     await buffer.CopyToAsync(original,context.RequestAborted);return;
    }
    using var reader=new StreamReader(buffer,Encoding.UTF8,detectEncodingFromByteOrderMarks:true,leaveOpen:true);
    var html=await reader.ReadToEndAsync(context.RequestAborted);
    const string tag="<script defer src=\"../NfoTrailer/Script.js\"></script>";
    var i=html.LastIndexOf("</head>",StringComparison.OrdinalIgnoreCase);
    if(i>=0 && !html.Contains("NfoTrailer/Script.js",StringComparison.OrdinalIgnoreCase))html=html.Insert(i,tag);
    var data=Encoding.UTF8.GetBytes(html);
    context.Response.ContentLength=data.Length;
    context.Response.Headers.CacheControl="no-store";
    context.Response.Headers.Remove("ETag");
    await original.WriteAsync(data,context.RequestAborted);
   } finally {context.Response.Body=original;}
  });
  next(app);
 };
}