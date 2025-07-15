using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Middleware.Extensions;
using Credfeto.Nuget.Proxy.Middleware.LoggingExtensions;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Bulkhead;
using Polly.Timeout;

namespace Credfeto.Nuget.Proxy.Middleware;

public sealed class JsonMiddleware : IMiddleware
{
    private static readonly IReadOnlyList<string> WhiteListedPaths = [
        "/autocomplete/query",
        "/search/query"];
    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly ILogger<JsonMiddleware> _logger;
    private readonly IJsonTransformer _jsonTransformer;

    public JsonMiddleware(IOptions<ProxyServerConfig> config, IEnumerable<IJsonTransformer> jsonTransformer, ICurrentTimeSource currentTimeSource, ILogger<JsonMiddleware> logger)
    {
        this._jsonTransformer = jsonTransformer.First(t => t.IsNuget == config.Value.IsNugetPublicServer);
        this._currentTimeSource = currentTimeSource;
        this._logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.GetEndpoint() is not null || !IsMatchingRequest(context: context, out string? path))
        {
            await next(context);

            return;
        }

        CancellationToken cancellationToken = context.RequestAborted;

        ProductInfoHeaderValue? userAgent = context.GetUserAgent();

        try
        {
            JsonResult? result = await this._jsonTransformer.GetFromUpstreamAsync(path: path, userAgent: userAgent, cancellationToken: cancellationToken);

            if (result is null)
            {
                this._logger.NoUpstreamJson(path);
                await next(context);

                return;
            }

            this._logger.FoundUpstreamJson(path: path, cacheSeconds: result.Value.CacheMaxAgeSeconds);
            int ageSeconds = result.Value.CacheMaxAgeSeconds;
            string json = result.Value.Json;
            await this.SuccessAsync(context: context, json: json, ageSeconds: ageSeconds, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            HttpStatusCode errorCode = exception.StatusCode ?? HttpStatusCode.InternalServerError;

            if (errorCode == HttpStatusCode.NotFound)
            {
                this._logger.HttpNotFound(path: path);
                this.NotFound(context: context, result: errorCode);
            }
            else
            {
                this._logger.HttpError(path: path, statusCode: errorCode, message: exception.Message, exception: exception);

                Failed(context: context, result: errorCode);
            }
        }
        catch (JsonException exception)
        {
            this._logger.InvalidJson(path: path, message: exception.Message, exception: exception);
            Failed(context: context, result: HttpStatusCode.InternalServerError);
        }
        catch (Exception exception) when (exception is TimeoutRejectedException or BulkheadRejectedException)
        {
            this._logger.TooManyRequests(path: path, message: exception.Message, exception: exception);
            TooManyRequests(context);
        }
        catch (Exception exception)
        {
            this._logger.InternalServerError(path: path, message: exception.Message, exception: exception);
            Failed(context: context, result: HttpStatusCode.InternalServerError);
        }
    }

    private static bool IsMatchingRequest(HttpContext context, [NotNullWhen(true)] out string? path)
    {
        if (StringComparer.Ordinal.Equals(x: context.Request.Method, y: "GET") && context.Request.Path.HasValue && IsMatchingPath(context.Request.Path.Value))
        {
            path = context.Request.Path.Value;

            return true;
        }

        path = null;

        return false;
    }

    private static bool IsMatchingPath(string path)
    {
        return path.EndsWith(value: ".json", comparisonType: StringComparison.OrdinalIgnoreCase) || WhiteListedPaths.Contains(value: path, comparer: StringComparer.OrdinalIgnoreCase);
    }

    private async ValueTask SuccessAsync(HttpContext context, string json, int ageSeconds, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.Headers.Append(key: "Content-Type", value: "application/json");
        context.Response.Headers.CacheControl = $"public, must-revalidate, max-age={ageSeconds}";
        context.Response.Headers.Expires = this._currentTimeSource.UtcNow()
                                               .AddSeconds(ageSeconds)
                                               .ToString(format: "ddd, dd MMM yyyy HH:mm:ss 'GMT'", formatProvider: CultureInfo.InvariantCulture);
        await context.Response.WriteAsync(text: json, cancellationToken: cancellationToken);
    }

    private static void Failed(HttpContext context, HttpStatusCode result)
    {
        context.Response.StatusCode = (int)result;
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
    }

    private void NotFound(HttpContext context, HttpStatusCode result)
    {
        const int ageSeconds = 300;
        context.Response.StatusCode = (int)result;
        context.Response.Headers.CacheControl = $"public, must-revalidate, max-age={ageSeconds}";
        context.Response.Headers.Expires = this._currentTimeSource.UtcNow()
                                               .AddSeconds(ageSeconds)
                                               .ToString(format: "ddd, dd MMM yyyy HH:mm:ss 'GMT'", formatProvider: CultureInfo.InvariantCulture);
    }

    private static void TooManyRequests(HttpContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.Response.Headers.Append(key: "Retry-After", value: "5");
    }
}