using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Credfeto.Nuget.Proxy.Config;
using Credfeto.Nuget.Proxy.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Credfeto.Nuget.Proxy.Extensions;

internal static class HttpClientSetup
{
    private const int CONCURRENT_ACTIONS = 30;
    private const int QUEUED_ACTIONS = 10;
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollyTimeout = HttpTimeout.Add(TimeSpan.FromSeconds(1));

    public static IServiceCollection AddJsonClient(this IServiceCollection services,  ProxyServerConfig appConfig)
    {
        return services.AddHttpClient(name: JsonMiddleware.ClientName,
                                      configureClient: httpClient => InitializeJsonClient(upstreamUrl: appConfig.UpstreamUrls[0], httpClient: httpClient, httpTimeout: HttpTimeout))
                       .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                       .ConfigurePrimaryHttpMessageHandler(configureHandler: _ => new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
                       .AddPolicyHandler(Policy.BulkheadAsync<HttpResponseMessage>(maxParallelization: CONCURRENT_ACTIONS, maxQueuingActions: QUEUED_ACTIONS))
                       .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(PollyTimeout)).Services;
    }

    private static void InitializeJsonClient(Uri upstreamUrl, HttpClient httpClient, TimeSpan httpTimeout)
    {
        httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        httpClient.DefaultRequestVersion = HttpVersion.Version11;
        httpClient.BaseAddress = upstreamUrl;
        httpClient.DefaultRequestHeaders.Accept.Add(new(mediaType: "application/json"));
        httpClient.DefaultRequestHeaders.UserAgent.Add(new(new ProductHeaderValue(name: VersionInformation.Product, version: VersionInformation.Version)));
        httpClient.Timeout = httpTimeout;
    }

}