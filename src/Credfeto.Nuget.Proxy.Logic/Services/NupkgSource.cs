using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Nuget.Index.Transformer.Interfaces;
using Credfeto.Nuget.Package.Storage.Interfaces;
using Credfeto.Nuget.Proxy.Extensions;
using Credfeto.Nuget.Proxy.Logic.Services.LoggingExtensions;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Polly.Bulkhead;
using Polly.Timeout;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public sealed class NupkgSource : INupkgSource
{
    private readonly ProxyServerConfig _config;
    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly IPackageDownloader _packageDownloader;
    private readonly ILogger<NupkgSource> _logger;
    private readonly IPackageStorage _packageStorage;

    public NupkgSource(
        ProxyServerConfig config,
        IPackageStorage packageStorage,
        IPackageDownloader packageDownloader,
        ICurrentTimeSource currentTimeSource,
        ILogger<NupkgSource> logger
    )
    {
        this._config = config;
        this._packageStorage = packageStorage;
        this._packageDownloader = packageDownloader;
        this._currentTimeSource = currentTimeSource;
        this._logger = logger;
    }

    public async ValueTask<bool> GetFromUpstreamAsync(
        HttpContext context,
        string path,
        CancellationToken cancellationToken
    )
    {
        if (path.EndsWith(value: ".nupkg", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (
                    !await this.TryToGetFromCacheAsync(
                        context: context,
                        sourcePath: path,
                        cancellationToken: context.RequestAborted
                    )
                )
                {
                    await this.GetFromUpstream2Async(
                        context: context,
                        sourcePath: path,
                        cancellationToken: context.RequestAborted
                    );
                }

                return true;
            }
            catch (Exception exception)
                when (exception is TimeoutRejectedException or BulkheadRejectedException)
            {
                TooManyRequestsResponse(context);

                return true;
            }
        }

        return false;
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

    private async ValueTask GetFromUpstream2Async(
        HttpContext context,
        string sourcePath,
        CancellationToken cancellationToken
    )
    {
        Uri requestUri = this.GetRequestUri(context);

        try
        {
            byte[] data = await this._packageDownloader.ReadUpstreamAsync(
                requestUri: requestUri,
                cancellationToken: cancellationToken
            );

            this.OkHeaders(context);

            this._logger.UpstreamPackageOk(
                upstream: requestUri,
                statusCode: HttpStatusCode.OK,
                length: data.Length
            );

            await this._packageStorage.SaveFileAsync(
                sourcePath: sourcePath,
                buffer: data,
                cancellationToken: cancellationToken
            );
        }
        catch (HttpRequestException exception)
        {
            this.UpstreamFailed(
                context: context,
                requestUri: requestUri,
                statusCode: exception.StatusCode ?? HttpStatusCode.InternalServerError
            );
        }
    }

    private void UpstreamFailed(HttpContext context, Uri requestUri, HttpStatusCode statusCode)
    {
        this._logger.UpstreamPackageFailed(upstream: requestUri, statusCode: statusCode);
        context.Response.StatusCode = (int)statusCode;
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
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
        this._logger.CachedPackage(upstream: requestUri, length: stream.Position);
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
}
