using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Credfeto.Nuget.Proxy.Logic;

internal static class HttpClientSetup
{
    private const int CONCURRENT_ACTIONS = 60;
    private const int QUEUED_ACTIONS = 20;
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan PollyTimeout = HttpTimeout.Add(TimeSpan.FromSeconds(1));

    public static IServiceCollection AddJsonClient(this IServiceCollection services)
    {
        return services
            .AddHttpClient(
                name: HttpClientNames.Json,
                configureClient: httpClient =>
                    InitializeJsonClient(
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

    public static IServiceCollection AddNupkgClient(this IServiceCollection services)
    {
        return services
            .AddHttpClient(
                name: HttpClientNames.NugetPackage,
                configureClient: httpClient =>
                    InitializeNupkgClient(
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

    private static void InitializeJsonClient(HttpClient httpClient, in TimeSpan httpTimeout)
    {
        httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        httpClient.DefaultRequestVersion = HttpVersion.Version11;
        httpClient.DefaultRequestHeaders.Accept.Add(new(mediaType: "application/json"));
        httpClient.DefaultRequestHeaders.UserAgent.Add(
            new(new ProductHeaderValue(name: VersionInformation.Product, version: VersionInformation.Version))
        );
        httpClient.Timeout = httpTimeout;
    }

    private static void InitializeNupkgClient(HttpClient httpClient, in TimeSpan httpTimeout)
    {
        httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        httpClient.DefaultRequestVersion = HttpVersion.Version11;
        httpClient.DefaultRequestHeaders.Accept.Add(new(mediaType: "application/octet-stream"));
        httpClient.DefaultRequestHeaders.UserAgent.Add(
            new(new ProductHeaderValue(name: VersionInformation.Product, version: VersionInformation.Version))
        );
        httpClient.Timeout = httpTimeout;
    }
}
