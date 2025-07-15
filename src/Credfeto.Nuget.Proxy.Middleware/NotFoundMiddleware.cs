using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Credfeto.Nuget.Proxy.Middleware;

public sealed class NotFoundMiddleware : IMiddleware
{
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.GetEndpoint() is not null)
        {
            return next(context);
        }

        MethodNotAllowed(context);

        return Task.CompletedTask;
    }

    private static void MethodNotAllowed(HttpContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
    }
}