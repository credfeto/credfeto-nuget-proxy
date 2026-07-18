using System;
using System.Buffers.Binary;
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
    private static readonly byte[] Magic = [(byte)'N', (byte)'G', (byte)'J', (byte)'B'];
    private const byte FormatVersion = 1;
    private const int HeaderSize = 4 + 1 + 4;

    private readonly string _basePath;
    private readonly string _basePathWithSeparator;
    private readonly ILogger<FileSystemJsonStorage> _logger;

    public FileSystemJsonStorage(IOptions<ProxyServerConfig> config, ILogger<FileSystemJsonStorage> logger)
    {
        this._logger = logger;

        (this._basePath, this._basePathWithSeparator) = PathContainment.CreateBase(config.Value.Metadata);

        this.EnsureDirectoryExists(this._basePath);
    }

    public async ValueTask SaveAsync(
        Uri requestUri,
        JsonMetadata metadata,
        string jsonContent,
        CancellationToken cancellationToken
    )
    {
        if (
            !this.TryBuildJsonPath(
                sourceHost: requestUri.Host,
                path: requestUri.AbsolutePath,
                filename: out string jsonPath,
                dir: out string dir
            )
        )
        {
            return;
        }

        await this.WriteFileAsync(
            jsonPath: jsonPath,
            dir: dir,
            metadata: metadata,
            jsonContent: jsonContent,
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask WriteFileAsync(
        string jsonPath,
        string dir,
        JsonMetadata metadata,
        string jsonContent,
        CancellationToken cancellationToken
    )
    {
        string? tempPath = null;

        try
        {
            this.EnsureDirectoryExists(dir);

            byte[] metaBytes = SerializeMetadata(
                new(
                    etag: metadata.Etag ?? string.Empty,
                    size: metadata.ContentLength,
                    contentType: metadata.ContentType ?? string.Empty
                )
            );

            tempPath = Path.Combine(dir, Path.GetRandomFileName());

            await using (Stream stream = File.Create(tempPath))
            {
                WriteHeader(stream: stream, metaBytes: metaBytes);

                await stream.WriteAsync(buffer: metaBytes, cancellationToken: cancellationToken);

                await using BrotliStream brotli = new(stream: stream, compressionLevel: CompressionLevel.Optimal);
                await using StreamWriter writer = new(stream: brotli, encoding: Encoding.UTF8, leaveOpen: true);
                await writer.WriteAsync(jsonContent.AsMemory(), cancellationToken);
            }

            File.Move(sourceFileName: tempPath, destFileName: jsonPath, overwrite: true);
            tempPath = null;
        }
        catch (Exception exception)
        {
            this._logger.SaveFailed(filename: jsonPath, message: exception.Message, exception: exception);
        }
        finally
        {
            if (tempPath is not null)
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception exception)
                {
                    this._logger.TempFileDeletionFailed(
                        filename: tempPath,
                        message: exception.Message,
                        exception: exception
                    );
                }
            }
        }
    }

    public ValueTask<JsonMetadata?> LoadMetadataAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        if (
            !this.TryBuildJsonPath(
                sourceHost: requestUri.Host,
                path: requestUri.AbsolutePath,
                filename: out string jsonPath,
                dir: out _
            )
        )
        {
            return ValueTask.FromResult<JsonMetadata?>(null);
        }

        return this.ReadFromCacheAsync(
            jsonPath: jsonPath,
            readAsync: ReadMetadataFromFileAsync,
            cancellationToken: cancellationToken
        );
    }

    public ValueTask<(JsonMetadata metadata, string content)?> LoadAsync(
        Uri requestUri,
        CancellationToken cancellationToken
    )
    {
        if (
            !this.TryBuildJsonPath(
                sourceHost: requestUri.Host,
                path: requestUri.AbsolutePath,
                filename: out string jsonPath,
                dir: out _
            )
        )
        {
            return ValueTask.FromResult<(JsonMetadata metadata, string content)?>(null);
        }

        return this.ReadFromCacheAsync(
            jsonPath: jsonPath,
            readAsync: ReadFromFileAsync,
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask<T?> ReadFromCacheAsync<T>(
        string jsonPath,
        Func<string, CancellationToken, ValueTask<T?>> readAsync,
        CancellationToken cancellationToken
    )
        where T : struct
    {
        try
        {
            return await readAsync(jsonPath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
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
            this.DeleteCorrupt(jsonPath);
            this._logger.FailedToReadFileFromCache(
                filename: jsonPath,
                message: exception.Message,
                exception: exception
            );

            return null;
        }
    }

    private static async ValueTask<JsonMetadata?> ReadMetadataFromFileAsync(
        string jsonPath,
        CancellationToken cancellationToken
    )
    {
        await using Stream stream = File.OpenRead(path: jsonPath);

        JsonItem? item = await ReadHeaderAndMetadataAsync(stream: stream, cancellationToken: cancellationToken);

        if (item is null)
        {
            return null;
        }

        return new JsonMetadata(Etag: item.Etag, ContentLength: item.Size, ContentType: item.ContentType);
    }

    private static async ValueTask<(JsonMetadata metadata, string content)?> ReadFromFileAsync(
        string jsonPath,
        CancellationToken cancellationToken
    )
    {
        await using Stream stream = File.OpenRead(path: jsonPath);

        JsonItem? item = await ReadHeaderAndMetadataAsync(stream: stream, cancellationToken: cancellationToken);

        if (item is null)
        {
            return null;
        }

        string content = await DecompressBodyAsync(stream: stream, cancellationToken: cancellationToken);

        return string.IsNullOrWhiteSpace(content)
            ? null
            : (new JsonMetadata(Etag: item.Etag, ContentLength: item.Size, ContentType: item.ContentType), content);
    }

    private static async ValueTask<JsonItem?> ReadHeaderAndMetadataAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        byte[] header = new byte[HeaderSize];

        if (!await ReadExactAsync(stream: stream, buffer: header, cancellationToken: cancellationToken))
        {
            return null;
        }

        if (!IsMagicValid(header))
        {
            return null;
        }

        if (header[4] != FormatVersion)
        {
            return null;
        }

        uint metaLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(5));

        if (metaLength > 1024 * 1024)
        {
            return null;
        }

        byte[] metaBytes = new byte[metaLength];

        if (!await ReadExactAsync(stream: stream, buffer: metaBytes, cancellationToken: cancellationToken))
        {
            return null;
        }

        return JsonSerializer.Deserialize(utf8Json: metaBytes, jsonTypeInfo: FileSystemJsonContext.Default.JsonItem);
    }

    private static bool IsMagicValid(byte[] header)
    {
        return header[0] == Magic[0] && header[1] == Magic[1] && header[2] == Magic[2] && header[3] == Magic[3];
    }

    private static async ValueTask<bool> ReadExactAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken
    )
    {
        int totalRead = 0;

        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer: buffer.AsMemory(totalRead), cancellationToken: cancellationToken);

            if (read == 0)
            {
                return false;
            }

            totalRead += read;
        }

        return true;
    }

    private static async ValueTask<string> DecompressBodyAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using BrotliStream brotli = new(stream: stream, mode: CompressionMode.Decompress, leaveOpen: true);
        using StreamReader reader = new(stream: brotli, encoding: Encoding.UTF8, leaveOpen: true);

        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static byte[] SerializeMetadata(JsonItem item)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value: item, jsonTypeInfo: FileSystemJsonContext.Default.JsonItem);
    }

    private static void WriteHeader(Stream stream, byte[] metaBytes)
    {
        stream.Write(buffer: Magic);
        stream.WriteByte(FormatVersion);

        Span<byte> lengthBuffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(destination: lengthBuffer, value: (uint)metaBytes.Length);
        stream.Write(buffer: lengthBuffer);
    }

    private void DeleteCorrupt(string jsonPath)
    {
        try
        {
            File.Delete(jsonPath);
        }
        catch (Exception exception)
        {
            this._logger.CorruptFileDeletionFailed(
                filename: jsonPath,
                message: exception.Message,
                exception: exception
            );
        }
    }

    private bool TryBuildJsonPath(string sourceHost, string path, out string filename, out string dir)
    {
        return PathContainment.TryBuildContainedPath(
            basePath: this._basePath,
            basePathWithSeparator: this._basePathWithSeparator,
            segments: [sourceHost, path],
            filename: out filename,
            dir: out dir
        );
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
}
