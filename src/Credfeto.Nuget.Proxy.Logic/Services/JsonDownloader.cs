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
using Credfeto.Nuget.Proxy.Logic.Services.LoggingExtensions;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Models.Models;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public sealed class JsonDownloader : IJsonDownloader
{
    private readonly ProxyServerConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJsonStorage _jsonStorage;
    private readonly ILogger<JsonDownloader> _logger;

    public JsonDownloader(IOptions<ProxyServerConfig> config, IHttpClientFactory httpClientFactory, IJsonStorage jsonStorage, ILogger<JsonDownloader> logger)
    {
        this._config = config.Value;
        this._httpClientFactory = httpClientFactory;
        this._jsonStorage = jsonStorage;
        this._logger = logger;
    }

    public async ValueTask<string> ReadUpstreamAsync(Uri requestUri, ProductInfoHeaderValue? userAgent, CancellationToken cancellationToken)
    {
        HttpClient client = this.GetClient(userAgent);

        (JsonMetadata metadata, string content)? cached = await this._jsonStorage.LoadAsync(requestUri: requestUri, cancellationToken: cancellationToken);

        if (cached is not null && !string.IsNullOrWhiteSpace(cached.Value.metadata.Etag))
        {
            this._logger.PreviouslyCached(upstream: requestUri, etag: cached.Value.metadata.Etag);
            AddEtag(client: client, cached: cached.Value.metadata);
        }

        using (HttpResponseMessage result = await client.GetAsync(requestUri: requestUri, cancellationToken: cancellationToken))
        {
            if (cached is not null && result.StatusCode == HttpStatusCode.NotModified)
            {
                this._logger.ReturningCached(upstream: requestUri, metadata: cached.Value.metadata, httpStatus: result.StatusCode);

                return cached.Value.content;
            }

            if (!result.IsSuccessStatusCode)
            {
                return Failed(requestUri: requestUri, resultStatusCode: result.StatusCode);
            }

            string? eTag = ExtractHeaderValue(headers: result.Headers, name: "ETag");

            if (cached is not null && !string.IsNullOrEmpty(cached.Value.metadata.Etag) && !string.IsNullOrWhiteSpace(eTag) && StringComparer.Ordinal.Equals(x: eTag, y: cached.Value.metadata.Etag))
            {
                this._logger.ReturningCached(upstream: requestUri, metadata: cached.Value.metadata, httpStatus: result.StatusCode);

                return cached.Value.content;
            }

            string json = await result.Content.ReadAsStringAsync(cancellationToken: cancellationToken);

            JsonMetadata jsonMetadata = LoadMetadata(result: result, eTag: eTag, contentLength: json.Length);

            this._logger.Metadata(upstream: requestUri, metadata: jsonMetadata, httpStatus: result.StatusCode);

            await this.SaveToCacheAsync(requestUri: requestUri, jsonMetadata: jsonMetadata, json: json, cancellationToken: cancellationToken);

            return json;
        }
    }

    private static void AddEtag(HttpClient client, in JsonMetadata cached)
    {
        if (string.IsNullOrWhiteSpace(cached.Etag))
        {
            return;
        }

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

        await this._jsonStorage.SaveAsync(requestUri: requestUri, metadata: jsonMetadata, jsonContent: json, cancellationToken: cancellationToken);
    }

    private static JsonMetadata LoadMetadata(HttpResponseMessage result, string? eTag, long contentLength)
    {
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
                   .WithBaseAddress(new(this._config.UpstreamUrls[0]))
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