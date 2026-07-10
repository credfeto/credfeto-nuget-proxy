using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Logic.Services;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Package.Storage.FileSystem;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Credfeto.Nuget.Proxy.Logic.Benchmark.Tests;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
[SuppressMessage(
    category: "FunFair.CodeAnalysis",
    checkId: "FFS0012: Classes should be static, sealed or abstract",
    Justification = "BenchmarkDotNet requires an unsealed class to generate its benchmark harness subclass"
)]
public class NupkgSourceDownloadBenchmark
{
    private const int PayloadSizeBytes = 256 * 1024;

    private byte[] _payload = [];
    private string _tempDir = string.Empty;
    private int _counter;

    [GlobalSetup]
    public void GlobalSetup()
    {
        this._payload = new byte[PayloadSizeBytes];
        RandomNumberGenerator.Fill(this._payload);

        this._tempDir = Directory.CreateTempSubdirectory("nupkg-source-benchmark-").FullName;
    }

    [Benchmark]
    public async Task DownloadAndCacheMissAsync()
    {
        this._counter++;

        ProxyServerConfig config = new()
        {
            UpstreamUrls = ["https://upstream.example.org"],
            PublicUrl = "https://nuget.example.org",
            Packages = this._tempDir,
        };

        IOptions<ProxyServerConfig> options = Options.Create(config);
        IPackageStorage storage = new FileSystemPackageStorage(options, NullLogger<FileSystemPackageStorage>.Instance);
        IHttpClientFactory httpClientFactory = new FakeHttpClientFactory(this._payload);
        IPackageDownloader downloader = new PackageDownloader(options, httpClientFactory);
        NupkgSource nupkgSource = new(options, storage, downloader, NullLogger<NupkgSource>.Instance);

        string path = $"/packages/bench-{this._counter}.nupkg";

        await using (
            PackageResult? result = await nupkgSource.GetFromUpstreamAsync(
                path: path,
                userAgent: null,
                cancellationToken: CancellationToken.None
            )
        )
        {
            if (result?.UpstreamStream is Stream stream)
            {
                await stream.CopyToAsync(Stream.Null, CancellationToken.None);
            }
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        Directory.Delete(this._tempDir, recursive: true);
    }
}
