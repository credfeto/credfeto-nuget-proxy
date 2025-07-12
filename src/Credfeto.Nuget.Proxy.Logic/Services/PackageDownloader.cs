using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Logic.Extensions;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.Extensions.Options;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public sealed class PackageDownloader : IPackageDownloader
{
    private readonly ProxyServerConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public PackageDownloader(IOptions<ProxyServerConfig> config, IHttpClientFactory httpClientFactory)
    {
        this._config = config.Value;
        this._httpClientFactory = httpClientFactory;
    }

    public async ValueTask<byte[]> ReadUpstreamAsync(
        Uri requestUri,
        ProductInfoHeaderValue? userAgent,
        CancellationToken cancellationToken
    )
    {
        HttpClient client = this.GetClient(userAgent);

        using (
            HttpResponseMessage result = await client.GetAsync(
                requestUri: requestUri,
                cancellationToken: cancellationToken
            )
        )
        {
            return !result.IsSuccessStatusCode
                ? Failed(requestUri, result.StatusCode)
                : await result.Content.ReadAsByteArrayAsync(cancellationToken: cancellationToken);
        }
    }

    [DoesNotReturn]
    private static byte[] Failed(Uri requestUri, HttpStatusCode resultStatusCode)
    {
        throw new HttpRequestException(
            $"Failed to download {requestUri}: {resultStatusCode.GetName()}",
            inner: null,
            statusCode: resultStatusCode
        );
    }

    private HttpClient GetClient(ProductInfoHeaderValue? userAgent)
    {
        return this
            ._httpClientFactory.CreateClient(HttpClientNames.NugetPackage)
            .WithBaseAddress(this._config.UpstreamUrls[0])
            .WithUserAgent(userAgent);
    }
}
