using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Package.Storage.Interfaces;
using Credfeto.Nuget.Proxy.Models.Config;
using FunFair.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.Nuget.Package.Storage.FileSystem.Tests;

public sealed class FileSystemPackageStorageTests : LoggingFolderCleanupTestBase
{
    private readonly IPackageStorage _packageStorage;

    public FileSystemPackageStorageTests(ITestOutputHelper output)
        : base(output)
    {
        Uri upstream = new("https://upstream.example.org");
        Uri publicUri = new("https://nuget.example.org");
        ProxyServerConfig config = new([upstream], PublicUrl: publicUri, Packages: this.TempFolder);

        this._packageStorage = new FileSystemPackageStorage(
            config: config,
            this.GetTypedLogger<FileSystemPackageStorage>()
        );
    }

    [Fact]
    public async Task FileDoesNotExistAsync()
    {
        await using (
            Stream? stream = await this._packageStorage.ReadFileAsync(
                sourcePath: "doesnotexist",
                cancellationToken: CancellationToken.None
            )
        )
        {
            Assert.Null(stream);
        }
    }

    [Fact]
    public async Task FileExistsAsync()
    {
        await File.WriteAllTextAsync(
            Path.Combine(path1: this.TempFolder, path2: "file.txt"),
            contents: "test",
            cancellationToken: CancellationToken.None
        );

        await using (
            Stream? stream = await this._packageStorage.ReadFileAsync(
                sourcePath: "file.txt",
                cancellationToken: CancellationToken.None
            )
        )
        {
            Assert.NotNull(stream);
            Assert.Equal(4, stream.Length);
        }
    }
}
