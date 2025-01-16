using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Config;
using Credfeto.Nuget.Proxy.Extensions;
using Microsoft.AspNetCore.Http;

namespace Credfeto.Nuget.Proxy.Middleware;

public sealed class JsonMiddleware
{
    private readonly ProxyServerConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RequestDelegate _next;

    public JsonMiddleware(RequestDelegate next, ProxyServerConfig config, IHttpClientFactory httpClientFactory)
    {
        this._next = next;
        this._config = config;
        this._httpClientFactory = httpClientFactory;
    }

    public static string ClientName => "UpStreamJson";

    public async Task InvokeAsync(HttpContext context)
    {
        if (StringComparer.Ordinal.Equals(x: context.Request.Method, y: "GET") && context.Request.Path.HasValue &&
            context.Request.Path.Value.EndsWith(value: ".json", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            await this.GetFromUpstreamAsync(context: context, cancellationToken: context.RequestAborted);

            return;
        }

        await this._next(context);
    }

    private async Task GetFromUpstreamAsync(HttpContext context, CancellationToken cancellationToken)
    {
        HttpClient client = this._httpClientFactory.CreateClient(ClientName);
        client.BaseAddress = this._config.UpstreamUrl;

        Uri requestUri = new(this._config.UpstreamUrl.CleanUri() + context.Request.Path.Value);

        HttpResponseMessage result = await client.GetAsync(requestUri: requestUri, cancellationToken: cancellationToken);

        if (result.StatusCode != HttpStatusCode.OK)
        {
            context.Response.StatusCode = (int)result.StatusCode;
            await result.Content.CopyToAsync(stream: context.Response.Body, cancellationToken: cancellationToken);

            return;
        }

        string json = await result.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
        json = json.Replace(this._config.UpstreamUrl.CleanUri(), this._config.UpstreamUrl.CleanUri(), comparisonType: StringComparison.Ordinal);
        await context.Response.WriteAsync(text: json, cancellationToken: cancellationToken);
    }
}