using System;
using System.Net;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Interfaces;
using Microsoft.AspNetCore.Http;
using Polly.Bulkhead;
using Polly.Timeout;

namespace Credfeto.Nuget.Proxy.Middleware;

public sealed class JsonMiddleware
{
    private readonly IJsonTransformer _jsonTransformer;

    private readonly RequestDelegate _next;

    public JsonMiddleware(RequestDelegate next, IJsonTransformer jsonTransformer)
    {
        this._next = next;
        this._jsonTransformer = jsonTransformer;
    }

    public static string ClientName => "UpStreamJson";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!StringComparer.Ordinal.Equals(x: context.Request.Method, y: "GET"))
        {
            await this._next(context);

            return;
        }

        if (!context.Request.Path.HasValue)
        {
            await this._next(context);

            return;
        }

        try
        {
            if (await this._jsonTransformer.GetFromUpstreamAsync(context: context, path: context.Request.Path.Value, cancellationToken: context.RequestAborted))
            {
                return;
            }
        }
        catch (Exception exception) when (exception is TimeoutRejectedException or BulkheadRejectedException)
        {
            context.Response.Clear();
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers.Append(key: "Retry-After", value: "5");

            return;
        }

        await this._next(context);
    }
}