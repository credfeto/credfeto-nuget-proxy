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

public sealed class ApiNugetOrgJsonIndexTransformerTests : LoggingTestBase
{
    private static readonly ProxyServerConfig Config = new()
    {
        UpstreamUrls = ["https://api.nuget.org"],
        PublicUrl = "https://nuget.example.org",
        JsonMaxAgeSeconds = 60,
    };

    private const string NEEDED_TYPE = "SearchQueryService/3.0.0-beta";
    private const string NOT_NEEDED_TYPE = "SomeUnrecognisedType";

    private readonly IJsonDownloader _jsonDownloader;
    private readonly IJsonTransformer _transformer;

    public ApiNugetOrgJsonIndexTransformerTests(ITestOutputHelper output)
        : base(output)
    {
        this._jsonDownloader = GetSubstitute<IJsonDownloader>();
        this._transformer = new Credfeto.Nuget.Proxy.Logic.Services.ApiNugetOrgJsonIndexTransformer(
            Options.Create(Config),
            this._jsonDownloader,
            this.GetTypedLogger<Credfeto.Nuget.Proxy.Logic.Services.ApiNugetOrgJsonIndexTransformer>()
        );
    }

    [Fact]
    public void IsNuget_ReturnsTrue()
    {
        Assert.True(
            this._transformer.IsNuget,
            userMessage: "Expected IsNuget to be true for ApiNugetOrgJsonIndexTransformer"
        );
    }

    [Fact]
    public async Task GetFromUpstreamAsync_FiltersAndRewritesResources_WhenPathIsIndexAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string UPSTREAM_JSON =
            """{"version":"3.0.0","resources":[{"@id":"https://api.nuget.org/v3/query","@type":"SearchQueryService/3.0.0-beta"},{"@id":"https://api.nuget.org/v3/other","@type":"SomeUnrecognisedType"}]}""";

        this._jsonDownloader.ReadUpstreamAsync(
                requestUri: Arg.Any<Uri>(),
                userAgent: Arg.Any<ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResponse(Json: UPSTREAM_JSON, ETag: "\"etag1\""));

        JsonResult? result = await this._transformer.GetFromUpstreamAsync(
            path: "/v3/index.json",
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Contains(NEEDED_TYPE, result.Value.Json, StringComparison.Ordinal);
        Assert.DoesNotContain(NOT_NEEDED_TYPE, result.Value.Json, StringComparison.Ordinal);
        Assert.Contains("https://nuget.example.org", result.Value.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("https://api.nuget.org", result.Value.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFromUpstreamAsync_RewritesAzureSearchUrl_WhenPathIsIndexAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string UPSTREAM_JSON =
            """{"version":"3.0.0","resources":[{"@id":"https://azuresearch-ussc.nuget.org/autocomplete","@type":"SearchAutocompleteService/3.0.0-beta"}]}""";

        this._jsonDownloader.ReadUpstreamAsync(
                requestUri: Arg.Any<Uri>(),
                userAgent: Arg.Any<ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResponse(Json: UPSTREAM_JSON, ETag: "\"etag2\""));

        JsonResult? result = await this._transformer.GetFromUpstreamAsync(
            path: "/v3/index.json",
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Contains("https://nuget.example.org", result.Value.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("https://azuresearch-ussc.nuget.org", result.Value.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFromUpstreamAsync_KeepsUnknownSourceUrl_WhenResourceUrlDoesNotMatchUpstreamAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string UPSTREAM_JSON =
            """{"version":"3.0.0","resources":[{"@id":"https://other.example.com/query","@type":"SearchQueryService/3.0.0-beta"}]}""";

        this._jsonDownloader.ReadUpstreamAsync(
                requestUri: Arg.Any<Uri>(),
                userAgent: Arg.Any<ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResponse(Json: UPSTREAM_JSON, ETag: "\"etag3\""));

        JsonResult? result = await this._transformer.GetFromUpstreamAsync(
            path: "/v3/index.json",
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Contains("https://other.example.com/query", result.Value.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFromUpstreamAsync_FallsThrough_WhenPathIsNotIndexAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string UPSTREAM_JSON =
            """{"resources":[{"@id":"https://api.nuget.org/v3/catalog/0.json","@type":"Catalog/3.0.0"}]}""";

        this._jsonDownloader.ReadUpstreamAsync(
                requestUri: Arg.Any<Uri>(),
                userAgent: Arg.Any<ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResponse(Json: UPSTREAM_JSON, ETag: "\"etag4\""));

        JsonResult? result = await this._transformer.GetFromUpstreamAsync(
            path: "/v3/catalog/0.json",
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Contains("https://nuget.example.org", result.Value.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("https://api.nuget.org", result.Value.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFromUpstreamAsync_WithUserAgent_FiltersAndRewritesResourcesAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string UPSTREAM_JSON =
            """{"version":"3.0.0","resources":[{"@id":"https://api.nuget.org/v3/query","@type":"SearchQueryService/3.0.0-beta"}]}""";

        this._jsonDownloader.ReadUpstreamAsync(
                requestUri: Arg.Any<Uri>(),
                userAgent: Arg.Any<ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResponse(Json: UPSTREAM_JSON, ETag: "\"etag5\""));

        ProductInfoHeaderValue userAgent = new(productName: "NuGet", productVersion: "6.0");

        JsonResult? result = await this._transformer.GetFromUpstreamAsync(
            path: "/v3/index.json",
            userAgent: userAgent,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Contains("https://nuget.example.org", result.Value.Json, StringComparison.Ordinal);
    }
}
