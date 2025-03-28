using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
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

    protected Task UpstreamFailedAsync(
        HttpContext context,
        Uri requestUri,
        HttpResponseMessage result,
        in CancellationToken cancellationToken
    )
    {
        this._logger.UpstreamJsonFailed(upstream: requestUri, statusCode: result.StatusCode);
        context.Response.StatusCode = (int)result.StatusCode;
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";

        return result.Content.CopyToAsync(
            stream: context.Response.Body,
            cancellationToken: cancellationToken
        );
    }

    protected async Task DoGetFromUpstreamAsync(
        HttpContext context,
        string path,
        CancellationToken cancellationToken
    )
    {
        Uri requestUri = this.GetRequestUri(path);
        HttpResponseMessage result = await this.ReadUpstreamAsync(
            requestUri: requestUri,
            cancellationToken: cancellationToken
        );

        if (result.StatusCode != HttpStatusCode.OK)
        {
            await this.UpstreamFailedAsync(
                context: context,
                requestUri: requestUri,
                result: result,
                cancellationToken: cancellationToken
            );

            return;
        }

        string json = await result.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
        json = this.ReplaceUrls(json);
        this._logger.UpstreamJsonOk(
            upstream: requestUri,
            statusCode: result.StatusCode,
            length: json.Length
        );

        this.OkHeaders(context);
        await context.Response.WriteAsync(text: json, cancellationToken: cancellationToken);
    }

    protected ValueTask<HttpResponseMessage> ReadUpstreamAsync(
        Uri requestUri,
        in CancellationToken cancellationToken
    )
    {
        return this._jsonDownloader.ReadUpstreamAsync(
            requestUri: requestUri,
            cancellationToken: cancellationToken
        );
    }

    protected Uri GetRequestUri(string path)
    {
        return new(this.Config.UpstreamUrls[0].CleanUri() + path);
    }

    private string ReplaceUrls(string json)
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
