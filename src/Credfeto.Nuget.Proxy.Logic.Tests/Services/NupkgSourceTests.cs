using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Nuget.Proxy.Logic.Tests.Services;

public sealed class NupkgSourceTests : LoggingTestBase
{
    private static readonly ProxyServerConfig Config = new()
    {
        UpstreamUrls = ["https://api.nuget.org"],
        PublicUrl = "https://nuget.example.org",
    };

    private readonly IPackageDownloader _packageDownloader;
    private readonly IPackageStorage _packageStorage;
    private readonly INupkgSource _nupkgSource;

    public NupkgSourceTests(ITestOutputHelper output)
        : base(output)
    {
        this._packageStorage = GetSubstitute<IPackageStorage>();
        this._packageDownloader = GetSubstitute<IPackageDownloader>();

        this._nupkgSource = new Credfeto.Nuget.Proxy.Logic.Services.NupkgSource(
            Options.Create(Config),
            this._packageStorage,
            this._packageDownloader,
            this.GetTypedLogger<Credfeto.Nuget.Proxy.Logic.Services.NupkgSource>()
        );
    }

    [Fact]
    public async Task GetFromUpstreamAsync_ReturnsNull_WhenPathDoesNotEndInNupkgAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        PackageResult? result = await this._nupkgSource.GetFromUpstreamAsync(
            path: "/v3/index.json",
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task GetFromUpstreamAsync_ReturnsCachedData_WhenFileIsCachedAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        byte[] data = [1, 2, 3, 4];
        this._packageStorage.ReadFileAsync(
                sourcePath: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(data);

        PackageResult? result = await this._nupkgSource.GetFromUpstreamAsync(
            path: "/packages/test/1.0.0/test.1.0.0.nupkg",
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(expected: data, actual: result.Value.Data);
    }

    [Fact]
    public async Task GetFromUpstreamAsync_DownloadsAndCaches_WhenFileIsNotCachedAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        byte[] downloadedData = [5, 6, 7, 8];

        this._packageStorage.ReadFileAsync(
                sourcePath: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns((byte[]?)null);

        this._packageDownloader.ReadUpstreamAsync(
                requestUri: Arg.Any<System.Uri>(),
                userAgent: Arg.Any<ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(downloadedData);

        PackageResult? result = await this._nupkgSource.GetFromUpstreamAsync(
            path: "/packages/test/1.0.0/test.1.0.0.nupkg",
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(expected: downloadedData, actual: result.Value.Data);

        await this
            ._packageStorage.Received(1)
            .SaveFileAsync(
                sourcePath: Arg.Any<string>(),
                buffer: Arg.Any<byte[]>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }
}
