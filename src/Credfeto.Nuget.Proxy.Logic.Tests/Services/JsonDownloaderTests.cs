using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Models.Models;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Nuget.Proxy.Logic.Tests.Services;

public sealed class JsonDownloaderTests : LoggingTestBase
{
    private static readonly ProxyServerConfig Config = new()
    {
        UpstreamUrls = ["https://api.nuget.org"],
        PublicUrl = "https://nuget.example.org",
    };

    private static readonly Uri RequestUri = new("https://api.nuget.org/v3/index.json");
    private const string SAMPLE_JSON = """{"version":"3.0.0"}""";

    private readonly IJsonStorage _jsonStorage;

    public JsonDownloaderTests(ITestOutputHelper output)
        : base(output)
    {
        this._jsonStorage = GetSubstitute<IJsonStorage>();
    }

    [Fact]
    public async Task ReadUpstreamAsync_ReturnsJson_WhenNoCacheAndSuccessResponseAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        MockJsonStorageLoadMetadata(storage: this._jsonStorage, result: null);

        using TestHttpMessageHandler handler = new(CreateJsonResponse(SAMPLE_JSON, etag: "\"etag1\""));
        using HttpClient client = new(handler);
        IJsonDownloader downloader = this.CreateDownloader(client);

        JsonResponse result = await downloader.ReadUpstreamAsync(
            requestUri: RequestUri,
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: SAMPLE_JSON, actual: result.Json);
        Assert.NotEmpty(result.ETag);
    }

    [Fact]
    public async Task ReadUpstreamAsync_WithUserAgent_ReturnsJson_WhenNoCacheAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        MockJsonStorageLoadMetadata(storage: this._jsonStorage, result: null);

        using TestHttpMessageHandler handler = new(CreateJsonResponse(SAMPLE_JSON, etag: "\"etag2\""));
        using HttpClient client = new(handler);
        IJsonDownloader downloader = this.CreateDownloader(client);

        ProductInfoHeaderValue userAgent = new(productName: "NuGet", productVersion: "6.0");

        JsonResponse result = await downloader.ReadUpstreamAsync(
            requestUri: RequestUri,
            userAgent: userAgent,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: SAMPLE_JSON, actual: result.Json);
    }

    [Fact]
    public async Task ReadUpstreamAsync_ReturnsCached_WhenServerReturns304Async()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string ETAG = "\"etag-cached\"";
        JsonMetadata cachedMetadata = new(
            Etag: ETAG,
            ContentLength: SAMPLE_JSON.Length,
            ContentType: "application/json"
        );
        MockJsonStorageLoadMetadata(storage: this._jsonStorage, result: cachedMetadata);
        MockJsonStorageLoadResult(storage: this._jsonStorage, result: (cachedMetadata, SAMPLE_JSON));

        using TestHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotModified));
        using HttpClient client = new(handler);
        IJsonDownloader downloader = this.CreateDownloader(client);

        JsonResponse result = await downloader.ReadUpstreamAsync(
            requestUri: RequestUri,
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: SAMPLE_JSON, actual: result.Json);
        Assert.Equal(expected: ETAG, actual: result.ETag);
    }

    [Fact]
    public async Task ReadUpstreamAsync_ReturnsCached_WhenEtagMatchesAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string ETAG = "\"matching-etag\"";
        JsonMetadata cachedMetadata = new(
            Etag: ETAG,
            ContentLength: SAMPLE_JSON.Length,
            ContentType: "application/json"
        );
        MockJsonStorageLoadMetadata(storage: this._jsonStorage, result: cachedMetadata);
        MockJsonStorageLoadResult(storage: this._jsonStorage, result: (cachedMetadata, SAMPLE_JSON));

        using TestHttpMessageHandler handler = new(CreateJsonResponse(SAMPLE_JSON, etag: ETAG));
        using HttpClient client = new(handler);
        IJsonDownloader downloader = this.CreateDownloader(client);

        JsonResponse result = await downloader.ReadUpstreamAsync(
            requestUri: RequestUri,
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: SAMPLE_JSON, actual: result.Json);
        Assert.Equal(expected: ETAG, actual: result.ETag);
    }

    [Fact]
    public async Task ReadUpstreamAsync_ReturnsHashAsETag_WhenResponseHasNoEtagAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        MockJsonStorageLoadMetadata(storage: this._jsonStorage, result: null);

        using TestHttpMessageHandler handler = new(CreateJsonResponse(SAMPLE_JSON, etag: null));
        using HttpClient client = new(handler);
        IJsonDownloader downloader = this.CreateDownloader(client);

        JsonResponse result = await downloader.ReadUpstreamAsync(
            requestUri: RequestUri,
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: SAMPLE_JSON, actual: result.Json);
        Assert.NotEmpty(result.ETag);
    }

    [Fact]
    public async Task ReadUpstreamAsync_ThrowsHttpRequestException_WhenResponseIsNotSuccessfulAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        MockJsonStorageLoadMetadata(storage: this._jsonStorage, result: null);

        using TestHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound));
        using HttpClient client = new(handler);
        IJsonDownloader downloader = this.CreateDownloader(client);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            downloader
                .ReadUpstreamAsync(requestUri: RequestUri, userAgent: null, cancellationToken: cancellationToken)
                .AsTask()
        );
    }

    [Fact]
    public async Task ReadUpstreamAsync_WithCachedEtag_AddsIfNoneMatchHeaderAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string ETAG = "\"cached-etag\"";
        JsonMetadata cachedMetadata = new(
            Etag: ETAG,
            ContentLength: SAMPLE_JSON.Length,
            ContentType: "application/json"
        );
        MockJsonStorageLoadMetadata(storage: this._jsonStorage, result: cachedMetadata);
        MockJsonStorageLoadResult(storage: this._jsonStorage, result: null);

        const string NEW_JSON = """{"version":"3.0.1"}""";
        const string NEW_ETAG = "\"new-etag\"";
        using TestHttpMessageHandler handler = new(CreateJsonResponse(NEW_JSON, etag: NEW_ETAG));
        using HttpClient client = new(handler);
        IJsonDownloader downloader = this.CreateDownloader(client);

        JsonResponse result = await downloader.ReadUpstreamAsync(
            requestUri: RequestUri,
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: NEW_JSON, actual: result.Json);
        Assert.Equal(expected: NEW_ETAG, actual: result.ETag);
    }

    private static void MockJsonStorageLoadMetadata(IJsonStorage storage, JsonMetadata? result)
    {
        storage
            .LoadMetadataAsync(requestUri: Arg.Any<Uri>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(result);
    }

    private static void MockJsonStorageLoadResult(IJsonStorage storage, (JsonMetadata metadata, string content)? result)
    {
        storage.LoadAsync(requestUri: Arg.Any<Uri>(), cancellationToken: Arg.Any<CancellationToken>()).Returns(result);
    }

    private IJsonDownloader CreateDownloader(HttpClient client)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(name: Arg.Any<string>()).Returns(client);

        return new Credfeto.Nuget.Proxy.Logic.Services.JsonDownloader(
            Options.Create(Config),
            factory,
            this._jsonStorage,
            this.GetTypedLogger<Credfeto.Nuget.Proxy.Logic.Services.JsonDownloader>()
        );
    }

    private static HttpResponseMessage CreateJsonResponse(string json, string? etag)
    {
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, mediaType: "application/json"),
        };

        if (!string.IsNullOrEmpty(etag))
        {
            response.Headers.Add(name: "ETag", value: etag);
        }

        return response;
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public TestHttpMessageHandler(HttpResponseMessage response) => this._response = response;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(this._response);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this._response.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
