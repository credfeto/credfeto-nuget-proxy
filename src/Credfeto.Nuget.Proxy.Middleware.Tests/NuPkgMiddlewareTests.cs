using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using FunFair.Test.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly.Bulkhead;
using Polly.Timeout;
using Xunit;

namespace Credfeto.Nuget.Proxy.Middleware.Tests;

public sealed class NuPkgMiddlewareTests : LoggingTestBase
{
    private static readonly DateTimeOffset FixedTime = new(
        year: 2024,
        month: 1,
        day: 1,
        hour: 0,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero
    );

    public NuPkgMiddlewareTests(ITestOutputHelper output)
        : base(output) { }

    private static IHost BuildHost(INupkgSource nupkgSource)
    {
        ICurrentTimeSource timeSource = Substitute.For<ICurrentTimeSource>();
        timeSource.UtcNow().Returns(FixedTime);

        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                    services
                        .AddSingleton<NuPkgMiddleware>()
                        .AddSingleton(nupkgSource)
                        .AddSingleton(timeSource)
                        .AddLogging()
                );
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<NuPkgMiddleware>();
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

        INupkgSource nupkgSource = Substitute.For<INupkgSource>();
        ICurrentTimeSource timeSource = Substitute.For<ICurrentTimeSource>();
        timeSource.UtcNow().Returns(FixedTime);

        using IHost host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                    services
                        .AddSingleton<NuPkgMiddleware>()
                        .AddSingleton(nupkgSource)
                        .AddSingleton(timeSource)
                        .AddLogging()
                        .AddRouting()
                );
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseMiddleware<NuPkgMiddleware>();
                    app.UseEndpoints(endpoints =>
                        endpoints.MapGet(
                            "/packages/test.nupkg",
                            async ctx =>
                                await ctx.Response.WriteAsync(text: "endpoint", cancellationToken: ctx.RequestAborted)
                        )
                    );
                });
            })
            .StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/packages/test.nupkg", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_CallsNext_WhenRequestIsNotGetAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        INupkgSource nupkgSource = Substitute.For<INupkgSource>();
        using IHost host = BuildHost(nupkgSource);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.PostAsync(
            requestUri: new Uri(uriString: "/packages/test.nupkg", UriKind.Relative),
            content: null,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.Accepted, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_CallsNext_WhenPathContainsTraversalAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        INupkgSource nupkgSource = Substitute.For<INupkgSource>();
        using IHost host = BuildHost(nupkgSource);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/packages/../etc/passwd", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.Accepted, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_CallsNext_WhenSourceReturnsNullAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        INupkgSource nupkgSource = Substitute.For<INupkgSource>();
        nupkgSource
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns((PackageResult?)null);

        using IHost host = BuildHost(nupkgSource);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/packages/test.1.0.0.nupkg", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.Accepted, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns200WithData_WhenSourceReturnsPackageAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        byte[] data = [1, 2, 3, 4];
        INupkgSource nupkgSource = Substitute.For<INupkgSource>();
        nupkgSource
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(PackageResult.FromUpstream(new MemoryStream(data), data.Length));

        using IHost host = BuildHost(nupkgSource);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/packages/test.1.0.0.nupkg", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        Assert.Equal(expected: data, actual: body);
    }

    [Fact]
    public async Task InvokeAsync_Returns200WithData_WhenRequestHasUserAgentAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        byte[] data = [5, 6, 7];
        INupkgSource nupkgSource = Substitute.For<INupkgSource>();
        nupkgSource
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(PackageResult.FromUpstream(new MemoryStream(data), data.Length));

        using IHost host = BuildHost(nupkgSource);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(name: "User-Agent", value: "NuGet/6.0");
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/packages/test.1.0.0.nupkg", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsErrorStatus_WhenHttpRequestExceptionIsThrownAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        INupkgSource nupkgSource = Substitute.For<INupkgSource>();
        nupkgSource
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(
                new HttpRequestException(
                    message: "upstream error",
                    inner: null,
                    statusCode: HttpStatusCode.ServiceUnavailable
                )
            );

        using IHost host = BuildHost(nupkgSource);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/packages/test.1.0.0.nupkg", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.ServiceUnavailable, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns500_WhenJsonExceptionIsThrownAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        INupkgSource nupkgSource = Substitute.For<INupkgSource>();
        nupkgSource
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new System.Text.Json.JsonException("bad json"));

        using IHost host = BuildHost(nupkgSource);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/packages/test.1.0.0.nupkg", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.InternalServerError, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns429_WhenTimeoutRejectedExceptionIsThrownAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        INupkgSource nupkgSource = Substitute.For<INupkgSource>();
        nupkgSource
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new TimeoutRejectedException("timeout"));

        using IHost host = BuildHost(nupkgSource);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/packages/test.1.0.0.nupkg", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.TooManyRequests, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns429_WhenBulkheadRejectedExceptionIsThrownAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        INupkgSource nupkgSource = Substitute.For<INupkgSource>();
        nupkgSource
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new BulkheadRejectedException("bulkhead"));

        using IHost host = BuildHost(nupkgSource);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/packages/test.1.0.0.nupkg", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.TooManyRequests, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns500_WhenUnhandledExceptionIsThrownAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        INupkgSource nupkgSource = Substitute.For<INupkgSource>();
        nupkgSource
            .GetFromUpstreamAsync(
                path: Arg.Any<string>(),
                userAgent: Arg.Any<System.Net.Http.Headers.ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new InvalidOperationException("unexpected error"));

        using IHost host = BuildHost(nupkgSource);
        await host.StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/packages/test.1.0.0.nupkg", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.InternalServerError, actual: response.StatusCode);
    }
}
