using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem.Tests;

public sealed class FileSystemPackageStorageTests : LoggingFolderCleanupTestBase
{
    private readonly IPackageStorage _packageStorage;

    public FileSystemPackageStorageTests(ITestOutputHelper output)
        : base(output)
    {
        ProxyServerConfig config = new()
        {
            UpstreamUrls = ["https://upstream.example.org"],
            PublicUrl = "https://nuget.example.org",
            Packages = this.TempFolder,
            JsonMaxAgeSeconds = 60,
        };

        this._packageStorage = new FileSystemPackageStorage(
            Options.Create(config),
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

    [Fact]
    public async Task SaveFileAndReadItBackAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        byte[] content = [1, 2, 3, 4, 5];

        await this._packageStorage.SaveFileAsync(
            sourcePath: "saved.nupkg",
            buffer: content,
            cancellationToken: cancellationToken
        );

        byte[]? result = await this._packageStorage.ReadFileAsync(
            sourcePath: "saved.nupkg",
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(expected: content, actual: result);
    }

    [Fact]
    public void ConstructorCreatesNonExistentDirectory()
    {
        string newSubDir = Path.Combine(path1: this.TempFolder, path2: "newpackages");

        Assert.False(
            condition: Directory.Exists(newSubDir),
            userMessage: "Expected sub-directory to not exist before construction"
        );

        ProxyServerConfig config = new()
        {
            UpstreamUrls = ["https://upstream.example.org"],
            PublicUrl = "https://nuget.example.org",
            Packages = newSubDir,
            JsonMaxAgeSeconds = 60,
        };

        _ = new FileSystemPackageStorage(Options.Create(config), this.GetTypedLogger<FileSystemPackageStorage>());

        Assert.True(
            condition: Directory.Exists(newSubDir),
            userMessage: "Expected sub-directory to be created by constructor"
        );
    }
}
