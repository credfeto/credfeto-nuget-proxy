using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Logic.Extensions;
using Credfeto.Nuget.Proxy.Models.Config;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public sealed class JsonDownloader : IJsonDownloader
{
    private readonly ProxyServerConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public JsonDownloader(ProxyServerConfig config, IHttpClientFactory httpClientFactory)
    {
        this._config = config;
        this._httpClientFactory = httpClientFactory;
    }

    public async ValueTask<string> ReadUpstreamAsync(
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
            return result.IsSuccessStatusCode
                ? await result.Content.ReadAsStringAsync(cancellationToken: cancellationToken)
                : Failed(requestUri: requestUri, resultStatusCode: result.StatusCode);
        }
    }

    [DoesNotReturn]
    private static string Failed(Uri requestUri, HttpStatusCode resultStatusCode)
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
            ._httpClientFactory.CreateClient(HttpClientNames.Json)
            .WithBaseAddress(this._config.UpstreamUrls[0])
            .WithUserAgent(userAgent);
    }
}
