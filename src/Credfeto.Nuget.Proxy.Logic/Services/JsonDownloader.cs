using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
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

    public JsonDownloader(
        IOptions<ProxyServerConfig> config,
        IHttpClientFactory httpClientFactory,
        IJsonStorage jsonStorage,
        ILogger<JsonDownloader> logger
    )
    {
        this._config = config.Value;
        this._httpClientFactory = httpClientFactory;
        this._jsonStorage = jsonStorage;
        this._logger = logger;
    }

    public async ValueTask<JsonResponse> ReadUpstreamAsync(
        Uri requestUri,
        ProductInfoHeaderValue? userAgent,
        CancellationToken cancellationToken
    )
    {
        HttpClient client = this.GetClient(userAgent);

        JsonMetadata? cachedMetadata = await this._jsonStorage.LoadMetadataAsync(
            requestUri: requestUri,
            cancellationToken: cancellationToken
        );

        if (cachedMetadata.HasValue && !string.IsNullOrWhiteSpace(cachedMetadata.Value.Etag))
        {
            this._logger.PreviouslyCached(upstream: requestUri, etag: cachedMetadata.Value.Etag);
            AddEtag(client: client, cached: cachedMetadata.Value);
        }

        using HttpResponseMessage result = await client.GetAsync(
            requestUri: requestUri,
            cancellationToken: cancellationToken
        );

        return await this.BuildResponseAsync(
            requestUri: requestUri,
            cachedMetadata: cachedMetadata,
            result: result,
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask<JsonResponse> BuildResponseAsync(
        Uri requestUri,
        JsonMetadata? cachedMetadata,
        HttpResponseMessage result,
        CancellationToken cancellationToken
    )
    {
        if (result.StatusCode == HttpStatusCode.NotModified)
        {
            JsonResponse? notModified = await this.TryBuildCachedResponseAsync(
                requestUri: requestUri,
                httpStatus: result.StatusCode,
                cancellationToken: cancellationToken
            );

            if (notModified is not null)
            {
                return notModified.Value;
            }
        }

        if (!result.IsSuccessStatusCode)
        {
            return Failed(requestUri: requestUri, resultStatusCode: result.StatusCode);
        }

        string? eTag = ExtractHeaderValue(headers: result.Headers, name: "ETag");

        if (cachedMetadata.HasValue)
        {
            string? matchedETag = GetMatchedETag(cachedMetadata: cachedMetadata.Value, eTag: eTag);

            if (matchedETag is not null)
            {
                JsonResponse? eTagMatch = await this.TryBuildCachedResponseOnETagMatchAsync(
                    requestUri: requestUri,
                    cachedMetadata: cachedMetadata.Value,
                    cachedETag: matchedETag,
                    httpStatus: result.StatusCode,
                    cancellationToken: cancellationToken
                );

                if (eTagMatch is not null)
                {
                    return eTagMatch.Value;
                }
            }
        }

        return await this.DownloadAndCacheAsync(
            requestUri: requestUri,
            result: result,
            eTag: eTag,
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask<JsonResponse?> TryBuildCachedResponseAsync(
        Uri requestUri,
        HttpStatusCode httpStatus,
        CancellationToken cancellationToken
    )
    {
        (JsonMetadata metadata, string content)? cached = await this._jsonStorage.LoadAsync(
            requestUri: requestUri,
            cancellationToken: cancellationToken
        );

        if (cached is null)
        {
            return null;
        }

        this._logger.ReturningCached(upstream: requestUri, metadata: cached.Value.metadata, httpStatus: httpStatus);

        // ! Should always have ETag at this point
        return new JsonResponse(Json: cached.Value.content, ETag: cached.Value.metadata.Etag!);
    }

    private async ValueTask<JsonResponse?> TryBuildCachedResponseOnETagMatchAsync(
        Uri requestUri,
        JsonMetadata cachedMetadata,
        string cachedETag,
        HttpStatusCode httpStatus,
        CancellationToken cancellationToken
    )
    {
        (JsonMetadata metadata, string content)? cached = await this._jsonStorage.LoadAsync(
            requestUri: requestUri,
            cancellationToken: cancellationToken
        );

        if (cached is null)
        {
            return null;
        }

        this._logger.ReturningCached(upstream: requestUri, metadata: cachedMetadata, httpStatus: httpStatus);

        return new JsonResponse(Json: cached.Value.content, ETag: cachedETag);
    }

    private static string? GetMatchedETag(in JsonMetadata cachedMetadata, string? eTag)
    {
        if (string.IsNullOrEmpty(cachedMetadata.Etag) || string.IsNullOrWhiteSpace(eTag))
        {
            return null;
        }

        return StringComparer.Ordinal.Equals(x: eTag, y: cachedMetadata.Etag) ? cachedMetadata.Etag : null;
    }

    private async ValueTask<JsonResponse> DownloadAndCacheAsync(
        Uri requestUri,
        HttpResponseMessage result,
        string? eTag,
        CancellationToken cancellationToken
    )
    {
        string json = await result.Content.ReadAsStringAsync(cancellationToken: cancellationToken);

        JsonMetadata jsonMetadata = LoadMetadata(result: result, eTag: eTag, contentLength: json.Length);

        this._logger.Metadata(upstream: requestUri, metadata: jsonMetadata, httpStatus: result.StatusCode);

        await this.SaveToCacheAsync(
            requestUri: requestUri,
            jsonMetadata: jsonMetadata,
            json: json,
            cancellationToken: cancellationToken
        );

        return new(Json: json, ETag: string.IsNullOrEmpty(jsonMetadata.Etag) ? HashJson(json) : jsonMetadata.Etag);
    }

    private static string HashJson(string json)
    {
        return Base64Url.EncodeToString(SHA512.HashData(Encoding.UTF8.GetBytes(json)));
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
        return source.StartsWith('"') && source.EndsWith('"') ? source : "\"" + source + "\"";
    }

    private async ValueTask SaveToCacheAsync(
        Uri requestUri,
        JsonMetadata jsonMetadata,
        string json,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(jsonMetadata.Etag) || string.IsNullOrWhiteSpace(jsonMetadata.ContentType))
        {
            return;
        }

        if (!StringComparer.Ordinal.Equals(x: jsonMetadata.ContentType, y: "application/json"))
        {
            return;
        }

        await this._jsonStorage.SaveAsync(
            requestUri: requestUri,
            metadata: jsonMetadata,
            jsonContent: json,
            cancellationToken: cancellationToken
        );
    }

    private static JsonMetadata LoadMetadata(HttpResponseMessage result, string? eTag, long contentLength)
    {
        string? contentType = result.Content.Headers.ContentType?.MediaType;

        return new(Etag: eTag, ContentLength: contentLength, ContentType: contentType);
    }

    [DoesNotReturn]
    private static JsonResponse Failed(Uri requestUri, HttpStatusCode resultStatusCode)
    {
        throw new HttpRequestException(
            $"Failed to download {requestUri}: {resultStatusCode.GetName()}",
            inner: null,
            statusCode: resultStatusCode
        );
    }

    private HttpClient GetClient(ProductInfoHeaderValue? userAgent)
    {
        return this
            ._httpClientFactory.CreateClient(HttpClientNames.Json)
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
