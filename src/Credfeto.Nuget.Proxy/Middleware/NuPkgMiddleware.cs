using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Nuget.Package.Storage.Interfaces;
using Credfeto.Nuget.Proxy.Extensions;
using Credfeto.Nuget.Proxy.Middleware.LoggingExtensions;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Polly.Bulkhead;
using Polly.Timeout;

namespace Credfeto.Nuget.Proxy.Middleware;

[SuppressMessage(
    category: "Microsoft.Security",
    checkId: "CA3003: Potential Path injection",
    Justification = "Avoided by checking path above"
)]
[SuppressMessage(
    category: "SecurityCodeScan.VS2019",
    checkId: "SCS0018: Potential Path injection",
    Justification = "Avoided by checking path above"
)]
public sealed class NuPkgMiddleware
{
    private readonly ProxyServerConfig _config;
    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NuPkgMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly IPackageStorage _packageStorage;

    public NuPkgMiddleware(
        RequestDelegate next,
        ProxyServerConfig config,
        IPackageStorage packageStorage,
        IHttpClientFactory httpClientFactory,
        ICurrentTimeSource currentTimeSource,
        ILogger<NuPkgMiddleware> logger
    )
    {
        this._next = next;
        this._config = config;
        this._packageStorage = packageStorage;
        this._httpClientFactory = httpClientFactory;
        this._currentTimeSource = currentTimeSource;
        this._logger = logger;
    }

    public static string ClientName => "UpStreamNuPkg";

    public async Task InvokeAsync(HttpContext context)
    {
        if (
            StringComparer.Ordinal.Equals(x: context.Request.Method, y: "GET")
            && context.Request.Path.HasValue
        )
        {
            if (
                context.Request.Path.Value.Contains(
                    value: "../",
                    comparisonType: StringComparison.Ordinal
                )
            )
            {
                await this._next(context);

                return;
            }

            string sourcePath = context.Request.Path.Value;

            if (
                sourcePath.EndsWith(
                    value: ".nupkg",
                    comparisonType: StringComparison.OrdinalIgnoreCase
                )
            )
            {
                try
                {
                    if (
                        !await this.TryToGetFromCacheAsync(
                            context: context,
                            sourcePath: sourcePath,
                            cancellationToken: context.RequestAborted
                        )
                    )
                    {
                        await this.GetFromUpstreamAsync(
                            context: context,
                            sourcePath: sourcePath,
                            cancellationToken: context.RequestAborted
                        );
                    }

                    return;
                }
                catch (Exception exception)
                    when (exception is TimeoutRejectedException or BulkheadRejectedException)
                {
                    TooManyRequestsResponse(context);

                    return;
                }
            }
        }

        await this._next(context);
    }

    private static void TooManyRequestsResponse(HttpContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        context.Response.Headers.Append(key: "Retry-After", value: "5");
    }

    private async ValueTask<bool> TryToGetFromCacheAsync(
        HttpContext context,
        string sourcePath,
        CancellationToken cancellationToken
    )
    {
        await using (
            Stream? stream = await this._packageStorage.ReadFileAsync(
                sourcePath: sourcePath,
                cancellationToken: cancellationToken
            )
        )
        {
            if (stream is not null)
            {
                await this.ServeCachedFileAsync(
                    context: context,
                    stream: stream,
                    cancellationToken: cancellationToken
                );

                return true;
            }
        }

        return false;
    }

    private async ValueTask GetFromUpstreamAsync(
        HttpContext context,
        string sourcePath,
        CancellationToken cancellationToken
    )
    {
        Uri requestUri = this.GetRequestUri(context);
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

        await using (MemoryStream memoryStream = new())
        {
            await result.Content.CopyToAsync(
                stream: memoryStream,
                cancellationToken: cancellationToken
            );

            this.OkHeaders(context);

            byte[] buffer = memoryStream.ToArray();
            await context.Response.Body.WriteAsync(
                buffer: buffer,
                cancellationToken: cancellationToken
            );

            this._logger.UpstreamOk(
                upstream: requestUri,
                statusCode: result.StatusCode,
                length: buffer.Length
            );

            await this._packageStorage.SaveFileAsync(
                sourcePath: sourcePath,
                buffer: buffer,
                cancellationToken: cancellationToken
            );
        }
    }

    private Task UpstreamFailedAsync(
        HttpContext context,
        Uri requestUri,
        HttpResponseMessage result,
        CancellationToken cancellationToken
    )
    {
        this._logger.UpstreamFailed(upstream: requestUri, statusCode: result.StatusCode);
        context.Response.StatusCode = (int)result.StatusCode;
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";

        return result.Content.CopyToAsync(
            stream: context.Response.Body,
            cancellationToken: cancellationToken
        );
    }

    private async Task<HttpResponseMessage> ReadUpstreamAsync(
        Uri requestUri,
        CancellationToken cancellationToken
    )
    {
        HttpClient client = this.GetClient();

        HttpResponseMessage result = await client.GetAsync(
            requestUri: requestUri,
            cancellationToken: cancellationToken
        );

        return result;
    }

    private async Task ServeCachedFileAsync(
        HttpContext context,
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        Uri requestUri = this.GetRequestUri(context);

        this.OkHeaders(context);
        await stream.CopyToAsync(
            destination: context.Response.Body,
            cancellationToken: cancellationToken
        );
        this._logger.Cached(upstream: requestUri, length: stream.Position);
    }

    private void OkHeaders(HttpContext context)
    {
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.Headers.Append(key: "Content-Type", value: "application/octet-stream");
        context.Response.Headers.CacheControl = "public, max-age=63072000, immutable";
        context.Response.Headers.Expires = this
            ._currentTimeSource.UtcNow()
            .AddSeconds(63072000)
            .ToString(
                format: "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
                formatProvider: CultureInfo.InvariantCulture
            );
    }

    private Uri GetRequestUri(HttpContext context)
    {
        Uri requestUri = new(this._config.UpstreamUrls[0].CleanUri() + context.Request.Path.Value);

        return requestUri;
    }

    private HttpClient GetClient()
    {
        HttpClient client = this._httpClientFactory.CreateClient(ClientName);
        client.BaseAddress = this._config.UpstreamUrls[0];

        return client;
    }
}
