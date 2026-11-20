using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
namespace Jellyfin.Plugin.RequestMovie.Services;
public sealed class FrontendInjection:IStartupFilter {
 public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)=>app=>{
  app.Use(async(ctx,middleware)=>{
   var p=ctx.Request.Path.Value??"";
   var web=p.EndsWith("/web/index.html",StringComparison.OrdinalIgnoreCase)||p.EndsWith("/web/",StringComparison.OrdinalIgnoreCase)||p.EndsWith("/web",StringComparison.OrdinalIgnoreCase);
   if(!HttpMethods.IsGet(ctx.Request.Method)||!web){await middleware(ctx);return;}
   var original=ctx.Response.Body;await using var buffer=new MemoryStream();ctx.Response.Body=buffer;
   try {
    await middleware(ctx);buffer.Position=0;
    if(ctx.Response.StatusCode!=200||ctx.Response.Headers.ContentEncoding.Count!=0){ctx.Response.ContentLength=buffer.Length;await buffer.CopyToAsync(original,ctx.RequestAborted);return;}
    using var reader=new StreamReader(buffer,Encoding.UTF8,detectEncodingFromByteOrderMarks:true,leaveOpen:true);
    var html=await reader.ReadToEndAsync(ctx.RequestAborted);
    const string script="<script defer src=\"../RequestMovie/Nav.js\"></script>";
    var i=html.LastIndexOf("</head>",StringComparison.OrdinalIgnoreCase);
    if(i>=0&&!html.Contains("RequestMovie/Nav.js",StringComparison.OrdinalIgnoreCase))html=html.Insert(i,script);
    var bytes=Encoding.UTF8.GetBytes(html);ctx.Response.ContentLength=bytes.Length;ctx.Response.Headers.CacheControl="no-store";ctx.Response.Headers.Remove("ETag");
    await original.WriteAsync(bytes,ctx.RequestAborted);
   }finally{ctx.Response.Body=original;}
  });next(app);
 };
}