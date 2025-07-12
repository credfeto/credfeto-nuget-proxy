using System;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Extensions;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Logic.Services.LoggingExtensions;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public sealed class NupkgSource : INupkgSource
{
    private readonly ProxyServerConfig _config;
    private readonly ILogger<NupkgSource> _logger;
    private readonly IPackageDownloader _packageDownloader;
    private readonly IPackageStorage _packageStorage;

    public NupkgSource(
        IOptions<ProxyServerConfig> config,
        IPackageStorage packageStorage,
        IPackageDownloader packageDownloader,
        ILogger<NupkgSource> logger
    )
    {
        this._config = config.Value;
        this._packageStorage = packageStorage;
        this._packageDownloader = packageDownloader;
        this._logger = logger;
    }

    public async ValueTask<PackageResult?> GetFromUpstreamAsync(
        string path,
        ProductInfoHeaderValue? userAgent,
        CancellationToken cancellationToken
    )
    {
        if (!path.EndsWith(value: ".nupkg", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await this.TryToGetFromCacheAsync(sourcePath: path, cancellationToken: cancellationToken)
            ?? await this.GetFromUpstream2Async(
                sourcePath: path,
                userAgent: userAgent,
                cancellationToken: cancellationToken
            );
    }

    private async ValueTask<PackageResult?> TryToGetFromCacheAsync(
        string sourcePath,
        CancellationToken cancellationToken
    )
    {
        byte[]? data = await this._packageStorage.ReadFileAsync(
            sourcePath: sourcePath,
            cancellationToken: cancellationToken
        );

        return data is null ? null : new(data);
    }

    private async ValueTask<PackageResult?> GetFromUpstream2Async(
        string sourcePath,
        ProductInfoHeaderValue? userAgent,
        CancellationToken cancellationToken
    )
    {
        Uri requestUri = this.GetRequestUri(sourcePath);

        byte[] data = await this._packageDownloader.ReadUpstreamAsync(
            requestUri: requestUri,
            userAgent: userAgent,
            cancellationToken: cancellationToken
        );

        this._logger.UpstreamPackageOk(upstream: requestUri, statusCode: HttpStatusCode.OK, length: data.Length);

        await this._packageStorage.SaveFileAsync(
            sourcePath: sourcePath,
            buffer: data,
            cancellationToken: cancellationToken
        );

        return new PackageResult(data);
    }

    private Uri GetRequestUri(string path)
    {
        return new(new Uri(this._config.UpstreamUrls[0]).CleanUri() + path);
    }
}
