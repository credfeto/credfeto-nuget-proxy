using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Nuget.Proxy.Extensions;
using Credfeto.Nuget.Proxy.Logic.Services.LoggingExtensions;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public abstract class JsonIndexTransformerBase
{
    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly IJsonDownloader _jsonDownloader;
    private readonly ILogger _logger;

    protected JsonIndexTransformerBase(
        ProxyServerConfig config,
        IJsonDownloader jsonDownloader,
        ICurrentTimeSource currentTimeSource,
        ILogger logger
    )
    {
        this.Config = config;
        this._jsonDownloader = jsonDownloader;
        this._currentTimeSource = currentTimeSource;
        this._logger = logger;
    }

    protected ProxyServerConfig Config { get; }

    protected void UpstreamFailed(HttpContext context, Uri requestUri, HttpStatusCode result)
    {
        this._logger.UpstreamJsonFailed(upstream: requestUri, statusCode: result);
        context.Response.StatusCode = (int)result;
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
    }

    protected async Task DoGetFromUpstreamAsync(
        HttpContext context,
        string path,
        Func<string, string> transformer,
        CancellationToken cancellationToken
    )
    {
        Uri requestUri = this.GetRequestUri(path);

        try
        {
            string json = await this._jsonDownloader.ReadUpstreamAsync(
                requestUri: requestUri,
                cancellationToken: cancellationToken
            );

            json = transformer(json);

            json = this.ReplaceUrls(json);
            this._logger.UpstreamJsonOk(
                upstream: requestUri,
                statusCode: HttpStatusCode.OK,
                length: json.Length
            );

            this.OkHeaders(context);
            await context.Response.WriteAsync(text: json, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            this.UpstreamFailed(
                context: context,
                requestUri: requestUri,
                result: exception.StatusCode ?? HttpStatusCode.InternalServerError
            );
        }
        catch (JsonException)
        {
            this.UpstreamFailed(
                context: context,
                requestUri: requestUri,
                result: HttpStatusCode.InternalServerError
            );
        }
    }

    protected Uri GetRequestUri(string path)
    {
        return new(this.Config.UpstreamUrls[0].CleanUri() + path);
    }

    protected string ReplaceUrls(string json)
    {
        return this.Config.UpstreamUrls.Aggregate(
            seed: json,
            func: (current, uri) =>
                current.Replace(
                    uri.CleanUri(),
                    this.Config.PublicUrl.CleanUri(),
                    comparisonType: StringComparison.Ordinal
                )
        );
    }

    protected void OkHeaders(HttpContext context)
    {
        int ageSeconds = this.GetJsonCacheMaxAge(context);

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.Headers.Append(key: "Content-Type", value: "application/json");
        context.Response.Headers.CacheControl = $"public, must-revalidate, max-age={ageSeconds}";
        context.Response.Headers.Expires = this
            ._currentTimeSource.UtcNow()
            .AddSeconds(ageSeconds)
            .ToString(
                format: "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
                formatProvider: CultureInfo.InvariantCulture
            );
    }

    private int GetJsonCacheMaxAge(HttpContext context)
    {
        if (
            context.Request.Path.HasValue
            && context.Request.Path.StartsWithSegments(
                other: "/v3/vulnerabilties",
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return this.Config.JsonMaxAgeSeconds * 10;
        }

        return this.Config.JsonMaxAgeSeconds;
    }
}
