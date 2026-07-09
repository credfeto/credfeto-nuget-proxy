using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Models.Config;
using FunFair.Test.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly.Bulkhead;
using Polly.Timeout;
using Xunit;

namespace Credfeto.Nuget.Proxy.Middleware.Tests;

public sealed class JsonMiddlewareTests : LoggingTestBase
{
    private static readonly ProxyServerConfig Config = new()
    {
        UpstreamUrls = [],
        PublicUrl = "https://nuget.example.org",
        JsonMaxAgeSeconds = 60,
    };

    private static readonly DateTimeOffset FixedTime = new(
        year: 2024,
        month: 1,
        day: 1,
        hour: 0,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero
    );

    public JsonMiddlewareTests(ITestOutputHelper output)
        : base(output) { }

    private static IHost BuildHost(IJsonTransformer jsonTransformer)
    {
        ICurrentTimeSource timeSource = Substitute.For<ICurrentTimeSource>();
        timeSource.UtcNow().Returns(FixedTime);

        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                    services
                        .AddSingleton<JsonMiddleware>()
                        .AddSingleton(Options.Create(Config))
                        .AddSingleton(jsonTransformer)
                        .AddSingleton(timeSource)
                        .AddLogging()
                );
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<JsonMiddleware>();
                    app.Run(static ctx =>
                    {
                        ctx.Response.StatusCode = (int)HttpStatusCode.Accepted;
                        return Task.CompletedTask;
                    });
                });
            })
            .Build();
    }

    [Fact]
    public async Task InvokeAsync_CallsNext_WhenEndpointIsSetAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);

        ICurrentTimeSource timeSource = Substitute.For<ICurrentTimeSource>();
        timeSource.UtcNow().Returns(FixedTime);

        using IHost host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                    services
                        .AddSingleton<JsonMiddleware>()
                        .AddSingleton(Options.Create(Config))
                        .AddSingleton(transformer)
                        .AddSingleton(timeSource)
                        .AddLogging()
                        .AddRouting()
                );
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseMiddleware<JsonMiddleware>();
                    app.UseEndpoints(endpoints =>
                        endpoints.MapGet(
                            "/v3/index.json",
                            async ctx =>
                                await ctx.Response.WriteAsync(text: "endpoint", cancellationToken: ctx.RequestAborted)
                        )
                    );
                });
            })
            .StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_CallsNext_WhenRequestIsNotGetAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.PostAsync(
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative),
            content: null,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.Accepted, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_CallsNext_WhenPathDoesNotMatchAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/packages/test.1.0.0.nupkg", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.Accepted, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_CallsNext_WhenTransformerReturnsNullAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns((JsonResult?)null);

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.Accepted, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns200WithJson_WhenTransformerSucceedsAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string JSON = """{"version":"3.0.0"}""";
        const string ETAG = "\"abc123\"";
        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResult(Json: JSON, CacheMaxAgeSeconds: 60, ETag: ETAG));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Equal(expected: JSON, actual: body);
        Assert.True(response.Headers.Contains("ETag"), userMessage: "ETag header should be present");
    }

    [Fact]
    public async Task InvokeAsync_Returns200_WhenEtagIsNotQuotedAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string JSON = """{"version":"3.0.0"}""";
        const string UNQUOTED_ETAG = "abc123";
        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResult(Json: JSON, CacheMaxAgeSeconds: 60, ETag: UNQUOTED_ETAG));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
        Assert.True(response.Headers.Contains("ETag"), userMessage: "ETag header should be present");
    }

    [Fact]
    public async Task InvokeAsync_Returns200_WhenWhitelistedPathMatchesAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string JSON = """{"results":[]}""";
        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResult(Json: JSON, CacheMaxAgeSeconds: 60, ETag: "\"etag\""));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/search/query", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns200_WhenUserAgentIsValidAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string JSON = """{"version":"3.0.0"}""";
        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResult(Json: JSON, CacheMaxAgeSeconds: 60, ETag: "\"etag\""));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(name: "User-Agent", value: "NuGet/6.0");
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns200_WhenUserAgentIsInvalidAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string JSON = """{"version":"3.0.0"}""";
        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResult(Json: JSON, CacheMaxAgeSeconds: 60, ETag: "\"etag\""));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(name: "User-Agent", value: "@invalid ua value");
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns304_WhenIfNoneMatchMatchesETagAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string JSON = """{"version":"3.0.0"}""";
        const string ETAG = "\"abc123\"";
        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResult(Json: JSON, CacheMaxAgeSeconds: 60, ETag: ETAG));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpRequestMessage request = new(
            method: HttpMethod.Get,
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative)
        );
        request.Headers.TryAddWithoutValidation(name: "If-None-Match", value: ETAG);
        using HttpResponseMessage response = await client.SendAsync(
            request: request,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.NotModified, actual: response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Empty(body);
    }

    [Fact]
    public async Task InvokeAsync_Returns304_WhenIfNoneMatchWeakMatchesStrongETagAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string JSON = """{"version":"3.0.0"}""";
        const string ETAG = "\"abc123\"";
        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResult(Json: JSON, CacheMaxAgeSeconds: 60, ETag: ETAG));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpRequestMessage request = new(
            method: HttpMethod.Get,
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative)
        );
        request.Headers.TryAddWithoutValidation(name: "If-None-Match", value: "W/" + ETAG);
        using HttpResponseMessage response = await client.SendAsync(
            request: request,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.NotModified, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns304_WhenIfNoneMatchIsWildcardAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string JSON = """{"version":"3.0.0"}""";
        const string ETAG = "\"abc123\"";
        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResult(Json: JSON, CacheMaxAgeSeconds: 60, ETag: ETAG));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpRequestMessage request = new(
            method: HttpMethod.Get,
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative)
        );
        request.Headers.TryAddWithoutValidation(name: "If-None-Match", value: "*");
        using HttpResponseMessage response = await client.SendAsync(
            request: request,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.NotModified, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns200_WhenIfNoneMatchDoesNotMatchAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string JSON = """{"version":"3.0.0"}""";
        const string ETAG = "\"abc123\"";
        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResult(Json: JSON, CacheMaxAgeSeconds: 60, ETag: ETAG));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpRequestMessage request = new(
            method: HttpMethod.Get,
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative)
        );
        request.Headers.TryAddWithoutValidation(name: "If-None-Match", value: "\"different\"");
        using HttpResponseMessage response = await client.SendAsync(
            request: request,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Equal(expected: JSON, actual: body);
    }

    [Fact]
    public async Task InvokeAsync_Returns304_WithETagAndCacheControlHeadersAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        const string JSON = """{"version":"3.0.0"}""";
        const string ETAG = "\"abc123\"";
        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new JsonResult(Json: JSON, CacheMaxAgeSeconds: 60, ETag: ETAG));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpRequestMessage request = new(
            method: HttpMethod.Get,
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative)
        );
        request.Headers.TryAddWithoutValidation(name: "If-None-Match", value: ETAG);
        using HttpResponseMessage response = await client.SendAsync(
            request: request,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.NotModified, actual: response.StatusCode);
        Assert.True(response.Headers.Contains("ETag"), userMessage: "ETag header should be present");
        Assert.True(response.Headers.CacheControl?.Public, userMessage: "Cache-Control should be public");
        Assert.True(
            response.Headers.CacheControl?.MustRevalidate,
            userMessage: "Cache-Control should require revalidation"
        );
    }

    [Fact]
    public async Task InvokeAsync_Returns404_WhenHttpRequestExceptionIs404Async()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(
                new HttpRequestException(message: "not found", inner: null, statusCode: HttpStatusCode.NotFound)
            );

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsErrorStatus_WhenHttpRequestExceptionIsNotFoundAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(
                new HttpRequestException(message: "error", inner: null, statusCode: HttpStatusCode.BadGateway)
            );

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.BadGateway, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns500_WhenJsonExceptionIsThrownAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new JsonException("bad json"));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.InternalServerError, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns429_WhenTimeoutRejectedExceptionIsThrownAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new TimeoutRejectedException("timeout"));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.TooManyRequests, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns429_WhenBulkheadRejectedExceptionIsThrownAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new BulkheadRejectedException("bulkhead"));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.TooManyRequests, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns500_WhenUnhandledExceptionIsThrownAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        IJsonTransformer transformer = Substitute.For<IJsonTransformer>();
        transformer.IsNuget.Returns(false);
        transformer
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new InvalidOperationException("unexpected error"));

        using IHost host = BuildHost(transformer);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/v3/index.json", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.InternalServerError, actual: response.StatusCode);
    }
}
