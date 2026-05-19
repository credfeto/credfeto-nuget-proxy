using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Credfeto.Nuget.Proxy.Middleware;

public sealed class ServerHeaderMiddleware : IMiddleware
{
    private static readonly string MachineName = Environment.MachineName;

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.OnStarting(AddServerHeaderAsync, state: context);

        return next(context);
    }

    private static Task AddServerHeaderAsync(object state)
    {
        HttpContext ctx = (HttpContext)state;
        ctx.Response.Headers.Append(key: "X-Server", value: MachineName);

        return Task.CompletedTask;
    }
}
