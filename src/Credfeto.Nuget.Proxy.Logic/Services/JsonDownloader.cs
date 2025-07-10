using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Logic.Extensions;
using Credfeto.Nuget.Proxy.Logic.Models;
using Credfeto.Nuget.Proxy.Logic.Services.LoggingExtensions;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public sealed class JsonDownloader : IJsonDownloader
{
    private readonly ProxyServerConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JsonDownloader> _logger;

    public JsonDownloader(ProxyServerConfig config, IHttpClientFactory httpClientFactory, ILogger<JsonDownloader> logger)
    {
        this._config = config;
        this._httpClientFactory = httpClientFactory;
        this._logger = logger;
    }

    public async ValueTask<string> ReadUpstreamAsync(Uri requestUri, ProductInfoHeaderValue? userAgent, CancellationToken cancellationToken)
    {
        HttpClient client = this.GetClient(userAgent);

        using (HttpResponseMessage result = await client.GetAsync(requestUri: requestUri, cancellationToken: cancellationToken))
        {
            if (!result.IsSuccessStatusCode)
            {
                return Failed(requestUri: requestUri, resultStatusCode: result.StatusCode);
            }

            JsonMetadata jsonMetadata = LoadMetadata(result.Headers);

            this._logger.Metadata(upstream: requestUri, metadata: jsonMetadata, httpStatus: result.StatusCode);

            return await result.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
        }
    }

    private static JsonMetadata LoadMetadata(HttpResponseHeaders headers)
    {
        string? eTag = ExtractHeaderValue(headers: headers, name: "etag");

        long contentLength = Convert.ToInt64(ExtractHeaderValue(headers: headers, name: "Content-Length") ?? "0", provider: CultureInfo.InvariantCulture);

        string? contentType = ExtractHeaderValue(headers: headers, name: "Content-Type");

        return new(Etag: eTag, ContentLength: contentLength, ContentType: contentType);
    }

    private static string? ExtractHeaderValue(HttpResponseHeaders headers, string name)
    {
        if (headers.TryGetValues(name: name, out IEnumerable<string>? values))
        {
            string? value = values.FirstOrDefault();

            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }

    [DoesNotReturn]
    private static string Failed(Uri requestUri, HttpStatusCode resultStatusCode)
    {
        throw new HttpRequestException($"Failed to download {requestUri}: {resultStatusCode.GetName()}", inner: null, statusCode: resultStatusCode);
    }

    private HttpClient GetClient(ProductInfoHeaderValue? userAgent)
    {
        return this._httpClientFactory.CreateClient(HttpClientNames.Json)
                   .WithBaseAddress(this._config.UpstreamUrls[0])
                   .WithUserAgent(userAgent);
    }
}