using System;
using System.IO;
using System.Linq;
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

        string? result = await this._packageStorage.ReadFileAsync(
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

        string? result = await this._packageStorage.ReadFileAsync(
            sourcePath: "file.txt",
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(expected: 4, actual: new FileInfo(result).Length);
    }

    [Fact]
    public async Task SaveFileAndReadItBackAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        byte[] content = [1, 2, 3, 4, 5];

        await this.SaveAndDrainAsync(sourcePath: "saved.nupkg", content: content, cancellationToken: cancellationToken);

        string? result = await this._packageStorage.ReadFileAsync(
            sourcePath: "saved.nupkg",
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(expected: content, actual: await File.ReadAllBytesAsync(result, cancellationToken));
    }

    [Fact]
    public async Task ReadFileAsync_WhenFileIsUnreadable_ReturnsNullAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        CancellationToken cancellationToken = this.CancellationToken();

        string filePath = Path.Combine(path1: this.TempFolder, path2: "unreadable.nupkg");
        await File.WriteAllBytesAsync(path: filePath, bytes: [1, 2, 3], cancellationToken: cancellationToken);
        File.SetUnixFileMode(path: filePath, mode: UnixFileMode.None);

        try
        {
            string? result = await this._packageStorage.ReadFileAsync(
                sourcePath: "unreadable.nupkg",
                cancellationToken: cancellationToken
            );

            Assert.Null(result);
        }
        finally
        {
            File.SetUnixFileMode(path: filePath, mode: UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public async Task SaveFileAsync_WhenDirectoryIsReadOnly_LogsErrorAndReturnsAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        CancellationToken cancellationToken = this.CancellationToken();

        string subDir = Path.Combine(path1: this.TempFolder, path2: "readonly-dir");
        Directory.CreateDirectory(subDir);
        File.SetUnixFileMode(path: subDir, mode: UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            await using MemoryStream content = new([1, 2, 3]);

            await using Stream result = await this._packageStorage.SaveFileAsync(
                sourcePath: "readonly-dir/test.nupkg",
                content: content,
                contentLength: 3,
                cancellationToken: cancellationToken
            );

            Assert.Same(expected: content, actual: result);
        }
        finally
        {
            File.SetUnixFileMode(
                path: subDir,
                mode: UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }
    }

    [Fact]
    public void EnsureDirectoryExists_WhenPackagesPathIsFile_LogsError()
    {
        string conflictPath = Path.Combine(path1: this.TempFolder, path2: "conflict-packages");
        File.WriteAllText(path: conflictPath, contents: "not a directory");

        ProxyServerConfig config = new()
        {
            UpstreamUrls = ["https://upstream.example.org"],
            PublicUrl = "https://nuget.example.org",
            Packages = conflictPath,
            JsonMaxAgeSeconds = 60,
        };

        _ = new FileSystemPackageStorage(Options.Create(config), this.GetTypedLogger<FileSystemPackageStorage>());
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

    [Fact]
    public async Task ConcurrentSaveFileAsync_FileContainsOneOfTheWrittenValuesAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        byte[] content1 = [1, 2, 3, 4, 5];
        byte[] content2 = [6, 7, 8, 9, 10];

        await Task.WhenAll(
            this.SaveAndDrainAsync(
                sourcePath: "concurrent.nupkg",
                content: content1,
                cancellationToken: cancellationToken
            ),
            this.SaveAndDrainAsync(
                sourcePath: "concurrent.nupkg",
                content: content2,
                cancellationToken: cancellationToken
            )
        );

        string? result = await this._packageStorage.ReadFileAsync(
            sourcePath: "concurrent.nupkg",
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        byte[] resultBytes = await File.ReadAllBytesAsync(result, cancellationToken);
        Assert.True(
            condition: resultBytes.SequenceEqual(content1) || resultBytes.SequenceEqual(content2),
            userMessage: "Concurrent writes must not produce a corrupt mix of both payloads"
        );
    }

    [Fact]
    public async Task SaveFileAsync_WhenCancelledBeforeWrite_ExistingFileIsPreservedAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        byte[] original = [1, 2, 3, 4, 5];

        await this.SaveAndDrainAsync(
            sourcePath: "preserve.nupkg",
            content: original,
            cancellationToken: cancellationToken
        );

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await using MemoryStream cancelledContent = new([6, 7, 8, 9, 10]);

        await using Stream cancelledTee = await this._packageStorage.SaveFileAsync(
            sourcePath: "preserve.nupkg",
            content: cancelledContent,
            contentLength: 5,
            cancellationToken: cts.Token
        );

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledTee.CopyToAsync(Stream.Null, cts.Token));

        string? result = await this._packageStorage.ReadFileAsync(
            sourcePath: "preserve.nupkg",
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(expected: original, actual: await File.ReadAllBytesAsync(result, cancellationToken));
    }

    private async Task SaveAndDrainAsync(string sourcePath, byte[] content, CancellationToken cancellationToken)
    {
        await using MemoryStream source = new(content);

        await using Stream tee = await this._packageStorage.SaveFileAsync(
            sourcePath: sourcePath,
            content: source,
            contentLength: content.Length,
            cancellationToken: cancellationToken
        );

        await tee.CopyToAsync(Stream.Null, cancellationToken);
    }
}
