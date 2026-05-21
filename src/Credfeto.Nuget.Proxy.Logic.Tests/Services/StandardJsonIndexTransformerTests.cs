using System;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Models.Config;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Nuget.Proxy.Logic.Tests.Services;

public sealed class StandardJsonIndexTransformerTests : LoggingTestBase
{
    private static readonly ProxyServerConfig Config = new()
    {
        UpstreamUrls = ["https://api.nuget.org"],
        PublicUrl = "https://nuget.example.org",
        JsonMaxAgeSeconds = 60,
    };

    private readonly IJsonDownloader _jsonDownloader;
    private readonly IJsonTransformer _transformer;

    public StandardJsonIndexTransformerTests(ITestOutputHelper output)
        : base(output)
    {
        this._jsonDownloader = GetSubstitute<IJsonDownloader>();
        this._transformer = new Credfeto.Nuget.Proxy.Logic.Services.StandardJsonIndexTransformer(
            Options.Create(Config),
            this._jsonDownloader,
            this.GetTypedLogger<Credfeto.Nuget.Proxy.Logic.Services.StandardJsonIndexTransformer>()
        );
    }

    [Fact]
    public void IsNuget_ReturnsFalse()
    {
        Assert.False(
            this._transformer.IsNuget,
            userMessage: "Expected IsNuget to be false for StandardJsonIndexTransformer"
        );
    }

    [Fact]
    public async Task GetFromUpstreamAsync_ReplacesUpstreamUrls_InResponseJsonAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string UPSTREAM_JSON =
            """{"resources":[{"@id":"https://api.nuget.org/v3/index.json","@type":"SearchQueryService"}]}""";

        this._jsonDownloader.ReadUpstreamAsync(
                requestUri: Arg.Any<Uri>(),
                userAgent: Arg.Any<ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResponse(Json: UPSTREAM_JSON, ETag: "\"etag1\""));

        JsonResult? result = await this._transformer.GetFromUpstreamAsync(
            path: "/v3/catalog/data.json",
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Contains("https://nuget.example.org", result.Value.Json, StringComparison.Ordinal);
        Assert.Equal(expected: 60, actual: result.Value.CacheMaxAgeSeconds);
    }

    [Fact]
    public async Task GetFromUpstreamAsync_ReturnsHigherMaxAge_WhenPathIsVulnerabilitiesAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        this._jsonDownloader.ReadUpstreamAsync(
                requestUri: Arg.Any<Uri>(),
                userAgent: Arg.Any<ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResponse(Json: """{}""", ETag: "\"etag2\""));

        JsonResult? result = await this._transformer.GetFromUpstreamAsync(
            path: "/v3/vulnerabilties/index.json",
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(expected: 600, actual: result.Value.CacheMaxAgeSeconds);
    }

    [Fact]
    public async Task GetFromUpstreamAsync_WithUserAgent_ReturnsJsonAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        this._jsonDownloader.ReadUpstreamAsync(
                requestUri: Arg.Any<Uri>(),
                userAgent: Arg.Any<ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResponse(Json: """{"data":"test"}""", ETag: "\"etag3\""));

        ProductInfoHeaderValue userAgent = new(productName: "NuGet", productVersion: "6.0");

        JsonResult? result = await this._transformer.GetFromUpstreamAsync(
            path: "/v3/catalog/data.json",
            userAgent: userAgent,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetFromUpstreamAsync_ReturnsUnchangedJson_WhenOneUpstreamUrlIsWhitespaceAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        ProxyServerConfig configWithEmpty = new()
        {
            UpstreamUrls = ["https://api.nuget.org", "  "],
            PublicUrl = "https://nuget.example.org",
            JsonMaxAgeSeconds = 60,
        };

        const string UPSTREAM_JSON = """{"data":"unchanged"}""";

        IJsonDownloader downloader = GetSubstitute<IJsonDownloader>();
        downloader
            .ReadUpstreamAsync(
                requestUri: Arg.Any<Uri>(),
                userAgent: Arg.Any<ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResponse(Json: UPSTREAM_JSON, ETag: "\"etag4\""));

        IJsonTransformer transformer = new Credfeto.Nuget.Proxy.Logic.Services.StandardJsonIndexTransformer(
            Options.Create(configWithEmpty),
            downloader,
            this.GetTypedLogger<Credfeto.Nuget.Proxy.Logic.Services.StandardJsonIndexTransformer>()
        );

        JsonResult? result = await transformer.GetFromUpstreamAsync(
            path: "/v3/something.json",
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Contains("unchanged", result.Value.Json, StringComparison.Ordinal);
    }
}
