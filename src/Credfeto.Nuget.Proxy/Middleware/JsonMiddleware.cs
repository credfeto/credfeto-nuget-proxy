using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Config;
using Credfeto.Nuget.Proxy.Extensions;
using Credfeto.Nuget.Proxy.Middleware.LoggingExtensions;
using Credfeto.Nuget.Proxy.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Middleware;

public sealed class JsonMiddleware
{
    private static readonly IReadOnlyList<string> NeededResources =
    [
        "SearchAutocompleteService/3.0.0-beta",
        "SearchQueryService/3.0.0-beta",
        "PackageBaseAddress/3.0.0",
        "RegistrationsBaseUrl/3.4.0"
    ];

    private readonly ProxyServerConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JsonMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly bool _publicNugetServer;

    public JsonMiddleware(RequestDelegate next, ProxyServerConfig config, IHttpClientFactory httpClientFactory, ILogger<JsonMiddleware> logger)
    {
        this._next = next;
        this._config = config;
        this._httpClientFactory = httpClientFactory;
        this._logger = logger;
        this._publicNugetServer = this._config.IsNugetPublicServer;
    }

    public static string ClientName => "UpStreamJson";

    public async Task InvokeAsync(HttpContext context)
    {
        if (StringComparer.Ordinal.Equals(x: context.Request.Method, y: "GET") && context.Request.Path.HasValue)
        {
            if (this._publicNugetServer && StringComparer.OrdinalIgnoreCase.Equals(x: context.Request.Path.Value, y: "/v3/index.json"))
            {
                await this.UpstreamIndexAsync(context: context, cancellationToken: context.RequestAborted);

                return;
            }

            if (context.Request.Path.Value.EndsWith(value: ".json", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                await this.GetFromUpstreamAsync(context: context, cancellationToken: context.RequestAborted);

                return;
            }
        }

        await this._next(context);
    }

    private async Task UpstreamIndexAsync(HttpContext context, CancellationToken cancellationToken)
    {
        Uri requestUri = this.GetRequestUri(context);

        HttpResponseMessage result = await this.ReadUpstreamAsync(cancellationToken: cancellationToken, requestUri: requestUri);

        if (result.StatusCode != HttpStatusCode.OK)
        {
            await this.UpstreamFailedAsync(context: context, requestUri: requestUri, result: result, cancellationToken: cancellationToken);

            return;
        }

        string json = await result.Content.ReadAsStringAsync(cancellationToken: cancellationToken);

        NugetResources? data = JsonSerializer.Deserialize<NugetResources>(json: json, jsonTypeInfo: AppJsonContexts.Default.NugetResources);

        if (data is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            return;
        }

        NugetResources resources = new(version: data.Version, [
            ..data.Resources.Where(IsNeeded)
                                       .Select( this.RewriteResource)
            ]
            );

        await SaveJsonResponseAsync(context: context, data: resources, cancellationToken: cancellationToken);
    }

    private static bool IsNeeded(NugetResource resource)
    {
        return NeededResources.Any(n => StringComparer.Ordinal.Equals(x: n, y: resource.Type));
    }

    [SuppressMessage("SonarAnalyzer.CSharp", "S3267: Use Linq", Justification = "Not Here")]
    private NugetResource RewriteResource(NugetResource resource)
    {
        foreach (Uri uri in this._config.UpstreamUrls)
        {
            if (resource.Id.StartsWith(uri.CleanUri(), StringComparison.OrdinalIgnoreCase))
            {
                return new(id: resource.Id.Replace(uri.CleanUri(), this._config.PublicUrl.CleanUri(), StringComparison.Ordinal), type: resource.Type, comment: resource.Comment);
            }
        }

        return resource;
    }

    private static Task SaveJsonResponseAsync(HttpContext context, NugetResources data, CancellationToken cancellationToken)
    {
        string result = JsonSerializer.Serialize(value: data, jsonTypeInfo: AppJsonContexts.Default.NugetResources);
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "application/json";

        return context.Response.WriteAsync(text: result, cancellationToken: cancellationToken);
    }

    private Task UpstreamFailedAsync(HttpContext context, Uri requestUri, HttpResponseMessage result, CancellationToken cancellationToken)
    {
        this._logger.UpstreamFailed(upstream: requestUri, statusCode: result.StatusCode);
        context.Response.StatusCode = (int)result.StatusCode;

        return result.Content.CopyToAsync(stream: context.Response.Body, cancellationToken: cancellationToken);
    }

    private HttpClient GetClient()
    {
        HttpClient client = this._httpClientFactory.CreateClient(ClientName);
        client.BaseAddress = this._config.UpstreamUrls[0];

        return client;
    }

    private async Task GetFromUpstreamAsync(HttpContext context, CancellationToken cancellationToken)
    {
        Uri requestUri = this.GetRequestUri(context);
        HttpResponseMessage result = await this.ReadUpstreamAsync(cancellationToken: cancellationToken, requestUri: requestUri);

        if (result.StatusCode != HttpStatusCode.OK)
        {
            await this.UpstreamFailedAsync(context: context, requestUri: requestUri, result: result, cancellationToken: cancellationToken);

            return;
        }

        string json = await result.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
        json = this.ReplaceUrls(json);
        this._logger.UpstreamOk(upstream: requestUri, statusCode: result.StatusCode, length: json.Length);
        await context.Response.WriteAsync(text: json, cancellationToken: cancellationToken);
    }

    private async Task<HttpResponseMessage> ReadUpstreamAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        HttpClient client = this.GetClient();

        HttpResponseMessage result = await client.GetAsync(requestUri: requestUri, cancellationToken: cancellationToken);

        return result;
    }

    private Uri GetRequestUri(HttpContext context)
    {
        Uri requestUri = new(this._config.UpstreamUrls[0]
                                 .CleanUri() + context.Request.Path.Value);

        return requestUri;
    }

    private string ReplaceUrls(string json)
    {
        json = this._config.UpstreamUrls.Aggregate(seed: json, func: (current, uri) => current.Replace(uri.CleanUri(), this._config.PublicUrl.CleanUri(), comparisonType: StringComparison.Ordinal));

        if (this._publicNugetServer)
        {
            json = json.Replace(oldValue: "https://www.nuget.org", this._config.PublicUrl.CleanUri(), comparisonType: StringComparison.Ordinal);
        }

        return json;
    }
}