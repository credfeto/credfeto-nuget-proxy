using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Middleware.Extensions;
using Microsoft.AspNetCore.Http;
using Polly.Bulkhead;
using Polly.Timeout;

namespace Credfeto.Nuget.Proxy.Middleware;

public sealed class JsonMiddleware : IMiddleware
{
    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly IJsonTransformer _jsonTransformer;

    public JsonMiddleware(IJsonTransformer jsonTransformer, ICurrentTimeSource currentTimeSource)
    {
        this._jsonTransformer = jsonTransformer;
        this._currentTimeSource = currentTimeSource;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!IsMatchingRequest(context: context, out string? path))
        {
            await next(context);

            return;
        }

        CancellationToken cancellationToken = context.RequestAborted;

        ProductInfoHeaderValue? userAgent = context.GetUserAgent();

        try
        {
            JsonResult? result = await this._jsonTransformer.GetFromUpstreamAsync(
                path: path,
                userAgent: userAgent,
                cancellationToken: cancellationToken
            );

            if (result is null)
            {
                await next(context);

                return;
            }

            int ageSeconds = result.Value.CacheMaxAgeSeconds;
            string json = result.Value.Json;
            await this.SuccessAsync(
                context: context,
                json: json,
                ageSeconds: ageSeconds,
                cancellationToken: cancellationToken
            );
        }
        catch (HttpRequestException exception)
        {
            Failed(context: context, exception.StatusCode ?? HttpStatusCode.InternalServerError);
        }
        catch (JsonException)
        {
            Failed(context: context, result: HttpStatusCode.InternalServerError);
        }
        catch (Exception exception) when (exception is TimeoutRejectedException or BulkheadRejectedException)
        {
            TooManyRequests(context);
        }
        catch (Exception)
        {
            await next(context);
        }
    }

    private static bool IsMatchingRequest(HttpContext context, [NotNullWhen(true)] out string? path)
    {
        if (
            StringComparer.Ordinal.Equals(x: context.Request.Method, y: "GET")
            && context.Request.Path.HasValue
            && context.Request.Path.Value.EndsWith(value: ".json", comparisonType: StringComparison.OrdinalIgnoreCase)
        )
        {
            path = context.Request.Path.Value;

            return true;
        }

        path = null;

        return false;
    }

    private async ValueTask SuccessAsync(
        HttpContext context,
        string json,
        int ageSeconds,
        CancellationToken cancellationToken
    )
    {
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.Headers.Append(key: "Content-Type", value: "application/json");
        context.Response.Headers.CacheControl = $"public, must-revalidate, max-age={ageSeconds}";
        context.Response.Headers.Expires = this
            ._currentTimeSource.UtcNow()
            .AddSeconds(ageSeconds)
            .ToString(format: "ddd, dd MMM yyyy HH:mm:ss 'GMT'", formatProvider: CultureInfo.InvariantCulture);
        await context.Response.WriteAsync(text: json, cancellationToken: cancellationToken);
    }

    private static void Failed(HttpContext context, HttpStatusCode result)
    {
        context.Response.StatusCode = (int)result;
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
    }

    private static void TooManyRequests(HttpContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.Response.Headers.Append(key: "Retry-After", value: "5");
    }
}