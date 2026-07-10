using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Logic.Services;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Nuget.Proxy.Logic.Tests.Services;

public sealed class NupkgSourceTests : LoggingFolderCleanupTestBase
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

        this._nupkgSource = new NupkgSource(
            Options.Create(Config),
            this._packageStorage,
            this._packageDownloader,
            this.GetTypedLogger<NupkgSource>()
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
        string cachedFilePath = Path.Combine(this.TempFolder, "cached.nupkg");

        await File.WriteAllBytesAsync(cachedFilePath, data, cancellationToken);

        this._packageStorage.ReadFileAsync(
                sourcePath: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(cachedFilePath);

        await using PackageResult? result = await this._nupkgSource.GetFromUpstreamAsync(
            path: "/packages/test/1.0.0/test.1.0.0.nupkg",
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(expected: cachedFilePath, actual: result.CachedFilePath);
        Assert.Equal(expected: data.Length, actual: result.ContentLength);
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
            .Returns((string?)null);

        using HttpResponseMessage httpResponse = new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(downloadedData),
        };
        UpstreamPackageResponse upstream = await UpstreamPackageResponse.CreateAsync(httpResponse, cancellationToken);

        this._packageDownloader.ReadUpstreamAsync(
                requestUri: Arg.Any<Uri>(),
                userAgent: Arg.Any<ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(upstream);

        this._packageStorage.SaveFileAsync(
                sourcePath: Arg.Any<string>(),
                content: Arg.Any<Stream>(),
                contentLength: Arg.Any<long?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(callInfo => callInfo.ArgAt<Stream>(1));

        await using PackageResult? result = await this._nupkgSource.GetFromUpstreamAsync(
            path: "/packages/test/1.0.0/test.1.0.0.nupkg",
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(expected: downloadedData.Length, actual: result.ContentLength);

        Stream? upstreamStream = result.UpstreamStream;
        Assert.NotNull(upstreamStream);

        await using MemoryStream buffer = new();
        await upstreamStream.CopyToAsync(buffer, cancellationToken);
        Assert.Equal(expected: downloadedData, actual: buffer.ToArray());

        await using Stream verifyStream = await this
            ._packageStorage.Received(1)
            .SaveFileAsync(
                sourcePath: Arg.Any<string>(),
                content: Arg.Any<Stream>(),
                contentLength: Arg.Any<long?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task GetFromUpstreamAsync_DisposesUpstreamStreamExactlyOnce_WhenCachingFailsAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        byte[] downloadedData = [9, 10, 11, 12];

        this._packageStorage.ReadFileAsync(
                sourcePath: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns((string?)null);

        DisposeCountingStream contentStream = new(new MemoryStream(downloadedData));

        using HttpResponseMessage httpResponse = new(HttpStatusCode.OK)
        {
            Content = new SpyHttpContent(stream: contentStream, length: downloadedData.Length),
        };
        UpstreamPackageResponse upstream = await UpstreamPackageResponse.CreateAsync(httpResponse, cancellationToken);

        this._packageDownloader.ReadUpstreamAsync(
                requestUri: Arg.Any<Uri>(),
                userAgent: Arg.Any<ProductInfoHeaderValue?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(upstream);

        // Simulates a cache-write failure: the storage layer hands back the same content stream, unwrapped.
        this._packageStorage.SaveFileAsync(
                sourcePath: Arg.Any<string>(),
                content: Arg.Any<Stream>(),
                contentLength: Arg.Any<long?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(callInfo => callInfo.ArgAt<Stream>(1));

        PackageResult? result = await this._nupkgSource.GetFromUpstreamAsync(
            path: "/packages/test/1.0.0/test.1.0.0.nupkg",
            userAgent: null,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);

        await result.DisposeAsync();

        Assert.Equal(expected: 1, actual: contentStream.DisposeCount);
    }

    private sealed class DisposeCountingStream : Stream
    {
        private readonly Stream _inner;

        public DisposeCountingStream(Stream inner)
        {
            this._inner = inner;
        }

        public int DisposeCount { get; private set; }

        public override bool CanRead => this._inner.CanRead;

        public override bool CanSeek => this._inner.CanSeek;

        public override bool CanWrite => this._inner.CanWrite;

        public override long Length => this._inner.Length;

        public override long Position
        {
            get => this._inner.Position;
            set => this._inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this._inner.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return this._inner.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this._inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this._inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this._inner.Write(buffer, offset, count);
        }

        public override void Flush()
        {
            this._inner.Flush();
        }

        public override async ValueTask DisposeAsync()
        {
            this.DisposeCount++;

            await this._inner.DisposeAsync();
            await base.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this._inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class SpyHttpContent : HttpContent
    {
        private readonly Stream _stream;

        public SpyHttpContent(Stream stream, long length)
        {
            this._stream = stream;
            this.Headers.ContentLength = length;
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return Task.FromResult(this._stream);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            throw new NotSupportedException();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = this._stream.Length;

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this._stream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
