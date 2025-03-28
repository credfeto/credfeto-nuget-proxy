using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Credfeto.Nuget.Proxy.Logic;

internal static class HttpClientSetup
{
    private const int CONCURRENT_ACTIONS = 30;
    private const int QUEUED_ACTIONS = 10;
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollyTimeout = HttpTimeout.Add(TimeSpan.FromSeconds(1));

    public static IServiceCollection AddJsonClient(
        this IServiceCollection services,
        ProxyServerConfig appConfig
    )
    {
        return services
            .AddHttpClient(
                name: HttpClientNames.Json,
                configureClient: httpClient =>
                    InitializeJsonClient(
                        upstreamUrl: appConfig.UpstreamUrls[0],
                        httpClient: httpClient,
                        httpTimeout: HttpTimeout
                    )
            )
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .ConfigurePrimaryHttpMessageHandler(configureHandler: _ => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
            })
            .AddPolicyHandler(
                Policy.BulkheadAsync<HttpResponseMessage>(
                    maxParallelization: CONCURRENT_ACTIONS,
                    maxQueuingActions: QUEUED_ACTIONS
                )
            )
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(PollyTimeout))
            .Services;
    }

    public static IServiceCollection AddNupkgClient(
        this IServiceCollection services,
        ProxyServerConfig appConfig
    )
    {
        return services
            .AddHttpClient(
                name: HttpClientNames.NugetPackage,
                configureClient: httpClient =>
                    InitializeNupkgClient(
                        upstreamUrl: appConfig.UpstreamUrls[0],
                        httpClient: httpClient,
                        httpTimeout: HttpTimeout
                    )
            )
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .ConfigurePrimaryHttpMessageHandler(configureHandler: _ => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
            })
            .AddPolicyHandler(
                Policy.BulkheadAsync<HttpResponseMessage>(
                    maxParallelization: CONCURRENT_ACTIONS * 2,
                    maxQueuingActions: QUEUED_ACTIONS * 2
                )
            )
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(PollyTimeout))
            .Services;
    }

    private static void InitializeJsonClient(
        Uri upstreamUrl,
        HttpClient httpClient,
        in TimeSpan httpTimeout
    )
    {
        httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        httpClient.DefaultRequestVersion = HttpVersion.Version11;
        httpClient.BaseAddress = upstreamUrl;
        httpClient.DefaultRequestHeaders.Accept.Add(new(mediaType: "application/json"));
        httpClient.DefaultRequestHeaders.UserAgent.Add(
            new(
                new ProductHeaderValue(
                    name: VersionInformation.Product,
                    version: VersionInformation.Version
                )
            )
        );
        httpClient.Timeout = httpTimeout;
    }

    private static void InitializeNupkgClient(
        Uri upstreamUrl,
        HttpClient httpClient,
        in TimeSpan httpTimeout
    )
    {
        httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        httpClient.DefaultRequestVersion = HttpVersion.Version11;
        httpClient.BaseAddress = upstreamUrl;
        httpClient.DefaultRequestHeaders.Accept.Add(new(mediaType: "application/octet-stream"));
        httpClient.DefaultRequestHeaders.UserAgent.Add(
            new(
                new ProductHeaderValue(
                    name: VersionInformation.Product,
                    version: VersionInformation.Version
                )
            )
        );
        httpClient.Timeout = httpTimeout;
    }
}
