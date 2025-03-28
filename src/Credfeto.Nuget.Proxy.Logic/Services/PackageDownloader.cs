using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Models.Config;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public sealed class PackageDownloader : IPackageDownloader
{
    private readonly ProxyServerConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public PackageDownloader(ProxyServerConfig config, IHttpClientFactory httpClientFactory)
    {
        this._config = config;
        this._httpClientFactory = httpClientFactory;
    }

    public async ValueTask<HttpResponseMessage> ReadUpstreamAsync(
        Uri requestUri,
        CancellationToken cancellationToken
    )
    {
        HttpClient client = this.GetClient();

        return await client.GetAsync(requestUri: requestUri, cancellationToken: cancellationToken);
    }

    private HttpClient GetClient()
    {
        HttpClient client = this._httpClientFactory.CreateClient(HttpClientNames.NugetPackage);
        client.BaseAddress = this._config.UpstreamUrls[0];

        return client;
    }
}
