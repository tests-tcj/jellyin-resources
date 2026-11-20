using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.RequestMovie.Services;

public sealed class FrontendInjection : IStartupFilter
{
	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
	{
		return (IApplicationBuilder app) =>
		{
			app.Use(async (HttpContext context, RequestDelegate nextMiddleware) =>
			{
				string text = context.Request.Path.Value ?? "";
				bool flag = text.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase) || text.EndsWith("/web/", StringComparison.OrdinalIgnoreCase) || text.EndsWith("/web", StringComparison.OrdinalIgnoreCase);
				if (!HttpMethods.IsGet(context.Request.Method) || !flag)
				{
					await nextMiddleware(context).ConfigureAwait(continueOnCapturedContext: false);
					return;
				}
				Stream originalStream = context.Response.Body;
				await using MemoryStream buffer = new MemoryStream();
				context.Response.Body = buffer;
				try
				{
					await nextMiddleware(context).ConfigureAwait(continueOnCapturedContext: false);
					buffer.Position = 0L;
					if (context.Response.StatusCode != 200 || context.Response.Headers.ContentEncoding.Count != 0)
					{
						context.Response.ContentLength = buffer.Length;
						await buffer.CopyToAsync(originalStream, context.RequestAborted).ConfigureAwait(continueOnCapturedContext: false);
					}
					else
					{
						using StreamReader reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, -1, leaveOpen: true);
						string text2 = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(continueOnCapturedContext: false);
						int num = text2.LastIndexOf("</head>", StringComparison.OrdinalIgnoreCase);
						if (num >= 0 && !text2.Contains("RequestMovie/Nav.js", StringComparison.OrdinalIgnoreCase))
						{
							text2 = text2.Insert(num, "<script defer src=\"../RequestMovie/Nav.js\"></script>");
						}
						byte[] bytes = Encoding.UTF8.GetBytes(text2);
						context.Response.ContentLength = bytes.Length;
						context.Response.Headers.CacheControl = "no-cache, max-age=0";
						context.Response.Headers.Remove("ETag");
						await originalStream.WriteAsync(bytes, context.RequestAborted).ConfigureAwait(continueOnCapturedContext: false);
					}
				}
				finally
				{
					context.Response.Body = originalStream;
				}
			});
			next(app);
		};
	}
}
