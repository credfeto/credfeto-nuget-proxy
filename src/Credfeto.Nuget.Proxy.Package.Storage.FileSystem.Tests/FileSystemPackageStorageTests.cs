using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem.Tests;

public sealed class FileSystemPackageStorageTests : LoggingFolderCleanupTestBase
{
    private readonly IPackageStorage _packageStorage;

    public FileSystemPackageStorageTests(ITestOutputHelper output)
        : base(output)
    {
        Uri upstream = new("https://upstream.example.org");
        Uri publicUri = new("https://nuget.example.org");
        ProxyServerConfig config = new(
            [upstream],
            PublicUrl: publicUri,
            Packages: this.TempFolder,
            JsonMaxAgeSeconds: 60
        );

        this._packageStorage = new FileSystemPackageStorage(
            config: config,
            this.GetTypedLogger<FileSystemPackageStorage>()
        );
    }

    [Fact]
    public async Task FileDoesNotExistAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        byte[]? result = await this._packageStorage.ReadFileAsync(
            sourcePath: "doesnotexist",
            cancellationToken: cancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task FileExistsAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        await File.WriteAllTextAsync(
            Path.Combine(path1: this.TempFolder, path2: "file.txt"),
            contents: "test",
            cancellationToken: cancellationToken
        );

        byte[]? result = await this._packageStorage.ReadFileAsync(
            sourcePath: "file.txt",
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(expected: 4, actual: result.Length);
    }
}
