using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Logic.Services;
using Credfeto.Nuget.Proxy.Models.Config;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Nuget.Proxy.Logic.Tests.Services;

public sealed class PackageDownloaderTests : LoggingTestBase
{
    private static readonly ProxyServerConfig Config = new()
    {
        UpstreamUrls = ["https://api.nuget.org"],
        PublicUrl = "https://nuget.example.org",
    };

    public PackageDownloaderTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task ReadUpstreamAsync_ReturnsBytes_WhenResponseIsSuccessfulAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        byte[] expected = [1, 2, 3, 4];

        using TestHttpMessageHandler handler = new(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(expected) }
        );
        using HttpClient client = new(handler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(name: Arg.Any<string>()).Returns(client);

        IPackageDownloader downloader = new PackageDownloader(Options.Create(Config), factory);

        await using UpstreamPackageResponse result = await downloader.ReadUpstreamAsync(
            requestUri: new Uri("https://api.nuget.org/packages/test.1.0.0.nupkg"),
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: expected.Length, actual: result.ContentLength);
        Assert.Equal(expected: expected, actual: await ReadAllAsync(result.Content, cancellationToken));
    }

    [Fact]
    public async Task ReadUpstreamAsync_WithUserAgent_ReturnsBytes_WhenResponseIsSuccessfulAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        byte[] expected = [5, 6, 7];

        using TestHttpMessageHandler handler = new(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(expected) }
        );
        using HttpClient client = new(handler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(name: Arg.Any<string>()).Returns(client);

        IPackageDownloader downloader = new PackageDownloader(Options.Create(Config), factory);

        ProductInfoHeaderValue userAgent = new(productName: "MyClient", productVersion: "1.0");

        await using UpstreamPackageResponse result = await downloader.ReadUpstreamAsync(
            requestUri: new Uri("https://api.nuget.org/packages/test.1.0.0.nupkg"),
            userAgent: userAgent,
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: expected, actual: await ReadAllAsync(result.Content, cancellationToken));
    }

    [Fact]
    public async Task ReadUpstreamAsync_ThrowsHttpRequestException_WhenResponseIsNotSuccessfulAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        using TestHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound));
        using HttpClient client = new(handler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(name: Arg.Any<string>()).Returns(client);

        IPackageDownloader downloader = new PackageDownloader(Options.Create(Config), factory);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            downloader
                .ReadUpstreamAsync(
                    requestUri: new Uri("https://api.nuget.org/packages/missing.1.0.0.nupkg"),
                    userAgent: null,
                    cancellationToken: cancellationToken
                )
                .AsTask()
        );
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using MemoryStream buffer = new();
        await stream.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
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
