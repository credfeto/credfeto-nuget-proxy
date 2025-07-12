using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Package.Storage.FileSystem.Json;
using Credfeto.Nuget.Proxy.Package.Storage.FileSystem.LoggingExtensions;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IO;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem;

public sealed class FileSystemJsonStorage : IJsonStorage
{
    private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new();
    private readonly string _basePath;
    private readonly ILogger<FileSystemJsonStorage> _logger;

    public FileSystemJsonStorage(IOptions<ProxyServerConfig> config, ILogger<FileSystemJsonStorage> logger)
    {
        this._logger = logger;

        this._basePath = config.Value.Metadata;

        this.EnsureDirectoryExists(this._basePath);
    }

    public async ValueTask<JsonItem?> LoadAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        if (
            !this.BuildJsonPath(
                sourceHost: requestUri.Host,
                path: requestUri.AbsolutePath,
                out string? jsonPath,
                dir: out _
            )
        )
        {
            return null;
        }

        try
        {
            if (!File.Exists(jsonPath))
            {
                return null;
            }

            await using (Stream content = File.OpenRead(path: jsonPath))
            {
                return await JsonSerializer.DeserializeAsync(
                    utf8Json: content,
                    jsonTypeInfo: FileSystemJsonContext.Default.JsonItem,
                    cancellationToken: cancellationToken
                );
            }
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

    public async ValueTask SaveAsync(Uri requestUri, JsonItem item, CancellationToken cancellationToken)
    {
        if (
            !this.BuildJsonPath(
                sourceHost: requestUri.Host,
                path: requestUri.AbsolutePath,
                out string? jsonPath,
                out string? dir
            )
        )
        {
            return;
        }

        try
        {
            this.EnsureDirectoryExists(dir);

            await using (RecyclableMemoryStream recyclableMemoryStream = MemoryStreamManager.GetStream())
            {
                PipeWriter pw = PipeWriter.Create(recyclableMemoryStream);
                await JsonSerializer.SerializeAsync(
                    utf8Json: pw,
                    value: item,
                    jsonTypeInfo: FileSystemJsonContext.Default.JsonItem,
                    cancellationToken: cancellationToken
                );

                await pw.FlushAsync(cancellationToken: cancellationToken);

                await File.WriteAllBytesAsync(
                    path: jsonPath,
                    recyclableMemoryStream.GetBuffer(),
                    cancellationToken: cancellationToken
                );
            }
        }
        catch (Exception exception)
        {
            this._logger.SaveFailed(filename: jsonPath, message: exception.Message, exception: exception);
        }
    }

    private bool BuildJsonPath(
        string sourceHost,
        string path,
        [NotNullWhen(true)] out string? filename,
        [NotNullWhen(true)] out string? dir
    )
    {
        string f = Path.Combine(path1: this._basePath, path2: sourceHost, path.TrimStart('/'));

        string? d = Path.GetDirectoryName(f);

        if (string.IsNullOrEmpty(d))
        {
            filename = null;
            dir = null;

            return false;
        }

        filename = f;
        dir = d;

        return true;
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
