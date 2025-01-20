using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Nuget.Proxy.Config;
using Credfeto.Nuget.Proxy.Extensions;
using Credfeto.Nuget.Proxy.Middleware;
using Credfeto.Nuget.Proxy.Services.LoggingExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Services;

public abstract class JsonIndexTransformerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly ILogger _logger;

    protected JsonIndexTransformerBase(ProxyServerConfig config, IHttpClientFactory httpClientFactory, ICurrentTimeSource currentTimeSource, ILogger logger)
    {
        this.Config = config;
        this._httpClientFactory = httpClientFactory;
        this._currentTimeSource = currentTimeSource;
        this._logger = logger;
    }

    protected ProxyServerConfig Config { get; }

    protected Task UpstreamFailedAsync(HttpContext context, Uri requestUri, HttpResponseMessage result, CancellationToken cancellationToken)
    {
        this._logger.UpstreamFailed(upstream: requestUri, statusCode: result.StatusCode);
        context.Response.StatusCode = (int)result.StatusCode;
        context.Response.Headers.CacheControl = $"no-cache, no-store, must-revalidate";

        return result.Content.CopyToAsync(stream: context.Response.Body, cancellationToken: cancellationToken);
    }

    private HttpClient GetClient()
    {
        HttpClient client = this._httpClientFactory.CreateClient(JsonMiddleware.ClientName);
        client.BaseAddress = this.Config.UpstreamUrls[0];

        return client;
    }

    protected async Task DoGetFromUpstreamAsync(HttpContext context, string path, CancellationToken cancellationToken)
    {
        Uri requestUri = this.GetRequestUri(path);
        HttpResponseMessage result = await this.ReadUpstreamAsync(cancellationToken: cancellationToken, requestUri: requestUri);

        if (result.StatusCode != HttpStatusCode.OK)
        {
            await this.UpstreamFailedAsync(context: context, requestUri: requestUri, result: result, cancellationToken: cancellationToken);

            return;
        }

        string json = await result.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
        json = this.ReplaceUrls(json);
        this._logger.UpstreamOk(upstream: requestUri, statusCode: result.StatusCode, length: json.Length);

        this.OkHeaders(context);
        await context.Response.WriteAsync(text: json, cancellationToken: cancellationToken);
    }

    protected async Task<HttpResponseMessage> ReadUpstreamAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        HttpClient client = this.GetClient();

        HttpResponseMessage result = await client.GetAsync(requestUri: requestUri, cancellationToken: cancellationToken);

        return result;
    }

    protected Uri GetRequestUri(string path)
    {
        return new(this.Config.UpstreamUrls[0]
                       .CleanUri() + path);
    }

    private string ReplaceUrls(string json)
    {
        return this.Config.UpstreamUrls.Aggregate(seed: json, func: (current, uri) => current.Replace(uri.CleanUri(), this.Config.PublicUrl.CleanUri(), comparisonType: StringComparison.Ordinal));
    }

    protected void OkHeaders(HttpContext context)
    {
        const int ageSeconds = 60 * 10;
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.Headers.Append(key: "Content-Type", value: "application/json");
        context.Response.Headers.CacheControl = $"public, must-revalidate, max-age={ageSeconds}";
        context.Response.Headers.Expires = this._currentTimeSource.UtcNow()
                                               .AddSeconds(ageSeconds)
                                               .ToString(format: "ddd, dd MMM yyyy HH:mm:ss 'GMT'", formatProvider: CultureInfo.InvariantCulture);
    }
}