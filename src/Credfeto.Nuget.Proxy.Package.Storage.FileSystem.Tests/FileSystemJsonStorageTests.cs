using System;
using System.Buffers.Text;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Models.Models;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem.Tests;

public sealed class FileSystemJsonStorageTests : LoggingFolderCleanupTestBase
{
    private readonly IJsonStorage _jsonStorage;

    public FileSystemJsonStorageTests(ITestOutputHelper output)
        : base(output)
    {
        ProxyServerConfig config = new()
        {
            UpstreamUrls = ["https://upstream.example.org"],
            PublicUrl = "https://nuget.example.org",
            Metadata = this.TempFolder,
            JsonMaxAgeSeconds = 60,
        };

        this._jsonStorage = new FileSystemJsonStorage(
            Options.Create(config),
            this.GetTypedLogger<FileSystemJsonStorage>()
        );
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsNullAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        Uri requestUri = new("https://api.nuget.org/v3/index.json");

        (JsonMetadata metadata, string content)? result = await this._jsonStorage.LoadAsync(
            requestUri: requestUri,
            cancellationToken: cancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAndLoadRoundtripAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        Uri requestUri = new("https://api.nuget.org/v3/roundtrip.json");

        const string JSON_CONTENT = """{"version":"3.0.0"}""";
        JsonMetadata metadata = new(
            Etag: "\"abc123\"",
            ContentLength: JSON_CONTENT.Length,
            ContentType: "application/json"
        );

        await this._jsonStorage.SaveAsync(
            requestUri: requestUri,
            metadata: metadata,
            jsonContent: JSON_CONTENT,
            cancellationToken: cancellationToken
        );

        (JsonMetadata metadata, string content)? result = await this._jsonStorage.LoadAsync(
            requestUri: requestUri,
            cancellationToken: cancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(expected: JSON_CONTENT, actual: result.Value.content);
        Assert.Equal(expected: "\"abc123\"", actual: result.Value.metadata.Etag);
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsCorrupt_ReturnsNullAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        Uri requestUri = new("https://api.nuget.org/v3/corrupted.json");

        string dir = Path.Combine(path1: this.TempFolder, path2: "api.nuget.org/v3");
        Directory.CreateDirectory(dir);
        string filePath = Path.Combine(path1: dir, path2: "corrupted.json");

        await File.WriteAllTextAsync(
            path: filePath,
            contents: "this is not valid json",
            cancellationToken: cancellationToken
        );

        (JsonMetadata metadata, string content)? result = await this._jsonStorage.LoadAsync(
            requestUri: requestUri,
            cancellationToken: cancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_WhenContentIsWhitespace_ReturnsNullAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        Uri requestUri = new("https://api.nuget.org/v3/whitespace.json");

        string dir = Path.Combine(path1: this.TempFolder, path2: "api.nuget.org/v3");
        Directory.CreateDirectory(dir);
        string filePath = Path.Combine(path1: dir, path2: "whitespace.json");

        string compressedWhitespace = await CompressToBase64UrlAsync("   ", cancellationToken);

        string jsonItem =
            $$$"""{"etag":"etag","size":3,"contentType":"application/json","content":"{{{compressedWhitespace}}}"}""";

        await File.WriteAllTextAsync(path: filePath, contents: jsonItem, cancellationToken: cancellationToken);

        (JsonMetadata metadata, string content)? result = await this._jsonStorage.LoadAsync(
            requestUri: requestUri,
            cancellationToken: cancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public void ConstructorCreatesNonExistentDirectory()
    {
        string newMetaDir = Path.Combine(path1: this.TempFolder, path2: "newmeta");

        Assert.False(
            condition: Directory.Exists(newMetaDir),
            userMessage: "Expected meta directory to not exist before construction"
        );

        ProxyServerConfig config = new()
        {
            UpstreamUrls = ["https://upstream.example.org"],
            PublicUrl = "https://nuget.example.org",
            Metadata = newMetaDir,
            JsonMaxAgeSeconds = 60,
        };

        _ = new FileSystemJsonStorage(Options.Create(config), this.GetTypedLogger<FileSystemJsonStorage>());

        Assert.True(
            condition: Directory.Exists(newMetaDir),
            userMessage: "Expected meta directory to be created by constructor"
        );
    }

    [Fact]
    public async Task LoadAsync_WhenDeserializedItemIsNull_ReturnsNullAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        Uri requestUri = new("https://api.nuget.org/v3/nullitem.json");

        string dir = Path.Combine(path1: this.TempFolder, path2: "api.nuget.org/v3");
        Directory.CreateDirectory(dir);
        string filePath = Path.Combine(path1: dir, path2: "nullitem.json");

        await File.WriteAllTextAsync(path: filePath, contents: "null", cancellationToken: cancellationToken);

        (JsonMetadata metadata, string content)? result = await this._jsonStorage.LoadAsync(
            requestUri: requestUri,
            cancellationToken: cancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsUnreadable_ReturnsNullAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        CancellationToken cancellationToken = this.CancellationToken();
        Uri requestUri = new("https://api.nuget.org/v3/unreadable.json");

        string dir = Path.Combine(path1: this.TempFolder, path2: "api.nuget.org/v3");
        Directory.CreateDirectory(dir);
        string filePath = Path.Combine(path1: dir, path2: "unreadable.json");

        await File.WriteAllTextAsync(path: filePath, contents: "content", cancellationToken: cancellationToken);
        File.SetUnixFileMode(path: filePath, mode: UnixFileMode.None);

        try
        {
            (JsonMetadata metadata, string content)? result = await this._jsonStorage.LoadAsync(
                requestUri: requestUri,
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
    public async Task LoadAsync_WhenCorruptFileCannotBeDeleted_ReturnsNullAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        CancellationToken cancellationToken = this.CancellationToken();
        Uri requestUri = new("https://api.nuget.org/v3/nodelete.json");

        string dir = Path.Combine(path1: this.TempFolder, path2: "api.nuget.org/v3");
        Directory.CreateDirectory(dir);
        string filePath = Path.Combine(path1: dir, path2: "nodelete.json");

        await File.WriteAllTextAsync(path: filePath, contents: "not valid json", cancellationToken: cancellationToken);
        File.SetUnixFileMode(path: dir, mode: UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            (JsonMetadata metadata, string content)? result = await this._jsonStorage.LoadAsync(
                requestUri: requestUri,
                cancellationToken: cancellationToken
            );

            Assert.Null(result);
        }
        finally
        {
            File.SetUnixFileMode(
                path: dir,
                mode: UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }
    }

    [Fact]
    public async Task SaveAsync_WhenDirectoryIsReadOnly_LogsErrorAndReturnsAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        CancellationToken cancellationToken = this.CancellationToken();

        string dir = Path.Combine(path1: this.TempFolder, path2: "api.nuget.org", path3: "v3");
        Directory.CreateDirectory(dir);
        File.SetUnixFileMode(path: dir, mode: UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            Uri requestUri = new("https://api.nuget.org/v3/readonly.json");
            const string JSON_CONTENT = """{"version":"3.0.0"}""";
            JsonMetadata metadata = new(
                Etag: "\"etag\"",
                ContentLength: JSON_CONTENT.Length,
                ContentType: "application/json"
            );

            await this._jsonStorage.SaveAsync(
                requestUri: requestUri,
                metadata: metadata,
                jsonContent: JSON_CONTENT,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            File.SetUnixFileMode(
                path: dir,
                mode: UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }
    }

    [Fact]
    public void EnsureDirectoryExists_WhenMetadataPathIsFile_LogsError()
    {
        string conflictPath = Path.Combine(path1: this.TempFolder, path2: "conflict-meta");
        File.WriteAllText(path: conflictPath, contents: "not a directory");

        ProxyServerConfig config = new()
        {
            UpstreamUrls = ["https://upstream.example.org"],
            PublicUrl = "https://nuget.example.org",
            Metadata = conflictPath,
            JsonMaxAgeSeconds = 60,
        };

        _ = new FileSystemJsonStorage(Options.Create(config), this.GetTypedLogger<FileSystemJsonStorage>());
    }

    private static async ValueTask<string> CompressToBase64UrlAsync(string source, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(source);

        await using MemoryStream output = new();

        await using (MemoryStream input = new(buffer: bytes, writable: false))
        {
            await using BrotliStream stream = new(stream: output, compressionLevel: CompressionLevel.SmallestSize);

            await input.CopyToAsync(destination: stream, cancellationToken: cancellationToken);
        }

        await output.FlushAsync(cancellationToken);

        return Base64Url.EncodeToString(output.ToArray());
    }
}
