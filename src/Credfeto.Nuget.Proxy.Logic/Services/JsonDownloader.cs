using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces.Models;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public sealed class JsonDownloader : IJsonDownloader
{
    private readonly ProxyServerConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJsonStorage _jsonStorage;
    private readonly ILogger<JsonDownloader> _logger;

    public JsonDownloader(ProxyServerConfig config, IHttpClientFactory httpClientFactory, IJsonStorage jsonStorage, ILogger<JsonDownloader> logger)
    {
        this._config = config;
        this._httpClientFactory = httpClientFactory;
        this._jsonStorage = jsonStorage;
        this._logger = logger;
    }

    public async ValueTask<string> ReadUpstreamAsync(Uri requestUri, ProductInfoHeaderValue? userAgent, CancellationToken cancellationToken)
    {
        HttpClient client = this.GetClient(userAgent);

        JsonItem? cached = await this._jsonStorage.LoadAsync(requestUri: requestUri, cancellationToken: cancellationToken);

        if (cached is not null && !string.IsNullOrWhiteSpace(cached.Etag))
        {
            this._logger.PreviouslyCached(upstream: requestUri, etag: cached.Etag);
            AddEtag(client: client, cached: cached);
        }

        using (HttpResponseMessage result = await client.GetAsync(requestUri: requestUri, cancellationToken: cancellationToken))
        {
            if (cached is not null && result.StatusCode == HttpStatusCode.NotModified)
            {
                this._logger.ReturningCached(upstream: requestUri, metadata: cached, httpStatus: result.StatusCode);

                return cached.Content;
            }

            if (!result.IsSuccessStatusCode)
            {
                return Failed(requestUri: requestUri, resultStatusCode: result.StatusCode);
            }

            string? eTag = ExtractHeaderValue(headers: result.Headers, name: "ETag");

            if (cached is not null && !string.IsNullOrEmpty(cached.Etag) && !string.IsNullOrWhiteSpace(eTag) && StringComparer.Ordinal.Equals(x: eTag, y: cached.Etag))
            {
                this._logger.ReturningCached(upstream: requestUri, metadata: cached, httpStatus: result.StatusCode);

                return cached.Content;
            }

            string json = await result.Content.ReadAsStringAsync(cancellationToken: cancellationToken);

            JsonMetadata jsonMetadata = LoadMetadata(result: result, eTag: eTag);

            this._logger.Metadata(upstream: requestUri, metadata: jsonMetadata, httpStatus: result.StatusCode);

            await this.SaveToCacheAsync(requestUri: requestUri, jsonMetadata: jsonMetadata, json: json, cancellationToken: cancellationToken);

            return json;
        }
    }

    private static void AddEtag(HttpClient client, JsonItem cached)
    {
        // TODO: If-None-Match: "33a64df551425fcc55e4d42a148795d9f25f89d4"
        string etag = EnsureQuoted(cached.Etag);

        client.DefaultRequestHeaders.Add(name: "If-None-Match", value: etag);
    }

    private static string EnsureQuoted(string source)
    {
        return source.StartsWith('"') && source.EndsWith('"')
            ? source
            : "\"" + source + "\"";
    }

    private async ValueTask SaveToCacheAsync(Uri requestUri, JsonMetadata jsonMetadata, string json, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jsonMetadata.Etag) || string.IsNullOrWhiteSpace(jsonMetadata.ContentType))
        {
            return;
        }

        if (!StringComparer.Ordinal.Equals(x: jsonMetadata.ContentType, y: "application/json"))
        {
            return;
        }

        await this._jsonStorage.SaveAsync(requestUri: requestUri,
                                          new(etag: jsonMetadata.Etag, size: jsonMetadata.ContentLength, contentType: jsonMetadata.ContentType, content: json),
                                          cancellationToken: cancellationToken);
    }

    private static JsonMetadata LoadMetadata(HttpResponseMessage result, string? eTag)
    {
        long contentLength = result.Content.Headers.ContentLength ?? 0;

        string? contentType = result.Content.Headers.ContentType?.MediaType;

        return new(Etag: eTag, ContentLength: contentLength, ContentType: contentType);
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
}