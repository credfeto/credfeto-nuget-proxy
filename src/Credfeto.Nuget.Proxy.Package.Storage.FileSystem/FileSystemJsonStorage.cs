using System;
using System.Buffers.Text;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Models.Models;
using Credfeto.Nuget.Proxy.Package.Storage.FileSystem.Json;
using Credfeto.Nuget.Proxy.Package.Storage.FileSystem.LoggingExtensions;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem;

public sealed class FileSystemJsonStorage : IJsonStorage
{
    private readonly string _basePath;
    private readonly ILogger<FileSystemJsonStorage> _logger;

    public FileSystemJsonStorage(IOptions<ProxyServerConfig> config, ILogger<FileSystemJsonStorage> logger)
    {
        this._logger = logger;

        this._basePath = config.Value.Metadata;

        this.EnsureDirectoryExists(this._basePath);
    }

    public async ValueTask SaveAsync(
        Uri requestUri,
        JsonMetadata metadata,
        string jsonContent,
        CancellationToken cancellationToken
    )
    {
        (string jsonPath, string dir) = this.BuildJsonPath(sourceHost: requestUri.Host, path: requestUri.AbsolutePath);

        try
        {
            this.EnsureDirectoryExists(dir);

            string compressed = await CompressAsync(source: jsonContent, cancellationToken: cancellationToken);

            JsonItem item = new(
                metadata.Etag ?? "",
                size: jsonContent.Length,
                metadata.ContentType ?? "",
                content: compressed
            );

            await using (Stream stream = File.OpenWrite(jsonPath))
            {
                await JsonSerializer.SerializeAsync(
                    utf8Json: stream,
                    value: item,
                    jsonTypeInfo: FileSystemJsonContext.Default.JsonItem,
                    cancellationToken: cancellationToken
                );
            }
        }
        catch (Exception exception)
        {
            this._logger.SaveFailed(filename: jsonPath, message: exception.Message, exception: exception);
        }
    }

    public async ValueTask<(JsonMetadata metadata, string content)?> LoadAsync(
        Uri requestUri,
        CancellationToken cancellationToken
    )
    {
        (string jsonPath, _) = this.BuildJsonPath(sourceHost: requestUri.Host, path: requestUri.AbsolutePath);

        try
        {
            if (!File.Exists(jsonPath))
            {
                return null;
            }

            return await ReadFromFileAsync(jsonPath: jsonPath, cancellationToken: cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            this._logger.FailedToReadFileFromCache(
                filename: jsonPath,
                message: exception.Message,
                exception: exception
            );

            return null;
        }
        catch (Exception exception)
        {
            DeleteCorrupt(jsonPath);
            this._logger.FailedToReadFileFromCache(
                filename: jsonPath,
                message: exception.Message,
                exception: exception
            );

            return null;
        }
    }

    private static async ValueTask<(JsonMetadata metadata, string content)?> ReadFromFileAsync(
        string jsonPath,
        CancellationToken cancellationToken
    )
    {
        await using Stream stream = File.OpenRead(path: jsonPath);

        JsonItem? item = await JsonSerializer.DeserializeAsync(
            utf8Json: stream,
            jsonTypeInfo: FileSystemJsonContext.Default.JsonItem,
            cancellationToken: cancellationToken
        );

        if (item is null)
        {
            return null;
        }

        JsonMetadata metadata = new(Etag: item.Etag, ContentLength: item.Size, ContentType: item.ContentType);
        string content = await DecompressAsync(source: item.Content, cancellationToken: cancellationToken);

        return string.IsNullOrWhiteSpace(content) ? null : (metadata, content);
    }

    private static void DeleteCorrupt(string jsonPath)
    {
        try
        {
            File.Delete(jsonPath);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Corrupt {exception.Message}");
        }
    }

    private (string filename, string dir) BuildJsonPath(string sourceHost, string path)
    {
        string f = Path.Combine(path1: this._basePath, path2: sourceHost, path.TrimStart('/'));

        // ! Path.Combine with an absolute basePath always produces a path with a directory component
        return (f, Path.GetDirectoryName(f)!);
    }

    private void EnsureDirectoryExists(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);
        }
        catch (Exception exception)
        {
            this._logger.SaveFailed(filename: folder, message: exception.Message, exception: exception);
        }
    }

    private static async ValueTask<string> CompressAsync(string source, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(source);

        await using (MemoryStream output = new())
        {
            await using (MemoryStream input = new(buffer: bytes, writable: false))
            {
                await using (BrotliStream stream = new(stream: output, compressionLevel: CompressionLevel.SmallestSize))
                {
                    await input.CopyToAsync(destination: stream, cancellationToken: cancellationToken);
                }
            }

            await output.FlushAsync(cancellationToken);

            return Base64Url.EncodeToString(output.ToArray());
        }
    }

    private static async ValueTask<string> DecompressAsync(string source, CancellationToken cancellationToken)
    {
        byte[] bytes = Base64Url.DecodeFromChars(source);

        await using (MemoryStream output = new())
        {
            await using (MemoryStream input = new(buffer: bytes, writable: true))
            {
                await using (BrotliStream stream = new(stream: input, mode: CompressionMode.Decompress))
                {
                    await stream.CopyToAsync(destination: output, cancellationToken: cancellationToken);
                }

                await output.FlushAsync(cancellationToken);
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }
    }
}
