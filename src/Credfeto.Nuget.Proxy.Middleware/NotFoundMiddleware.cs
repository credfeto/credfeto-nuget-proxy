using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Credfeto.Nuget.Proxy.Middleware;

public sealed class NotFoundMiddleware : IMiddleware
{
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (StringComparer.Ordinal.Equals(x: context.Request.Method, y: "GET"))
        {
            if (context.Request.Path.StartsWithSegments("/ping", StringComparison.OrdinalIgnoreCase))
            {
                return next(context);
            }

            NotFound(context);
            return Task.CompletedTask;
        }

        MethodNotAllowed(context);

        return Task.CompletedTask;
    }

    private static void NotFound(HttpContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
    }

    private static void MethodNotAllowed(HttpContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
    }
}
