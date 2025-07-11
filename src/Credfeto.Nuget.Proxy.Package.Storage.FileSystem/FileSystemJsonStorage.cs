using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Package.Storage.FileSystem.Json;
using Credfeto.Nuget.Proxy.Package.Storage.FileSystem.LoggingExtensions;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces.Models;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem;

public sealed class FileSystemJsonStorage : IJsonStorage
{
    private readonly string _basePath;
    private readonly ILogger<FileSystemJsonStorage> _logger;

    public FileSystemJsonStorage(ProxyServerConfig config, ILogger<FileSystemJsonStorage> logger)
    {
        this._logger = logger;

        this._basePath = Path.Combine(path1: config.Packages, path2: ".json");

        this.EnsureDirectoryExists(this._basePath);
    }

    public async ValueTask<JsonItem?> LoadAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        string jsonPath = this.BuildJsonPath(sourceHost: requestUri.Host, path: requestUri.AbsolutePath);

        try
        {
            if (File.Exists(jsonPath))
            {
                string content = await File.ReadAllTextAsync(path: jsonPath, cancellationToken: cancellationToken);

                return JsonSerializer.Deserialize(json: content, jsonTypeInfo: FileSystemJsonContext.Default.JsonItem);
            }

            return null;
        }
        catch (Exception exception)
        {
            this._logger.FailedToReadFileFromCache(
                filename: jsonPath,
                message: exception.Message,
                exception: exception
            );

            return null;
        }
    }

    public async ValueTask SaveAsync(Uri requestUri, JsonItem item, CancellationToken cancellationToken)
    {
        string jsonPath = this.BuildJsonPath(sourceHost: requestUri.Host, path: requestUri.AbsolutePath);

        // ! Doesn't
        string? dir = Path.GetDirectoryName(jsonPath);

        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        try
        {
            this.EnsureDirectoryExists(dir);

            string json = JsonSerializer.Serialize(value: item, jsonTypeInfo: FileSystemJsonContext.Default.JsonItem);

            await File.WriteAllTextAsync(path: jsonPath, contents: json, cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            this._logger.SaveFailed(filename: jsonPath, message: exception.Message, exception: exception);
        }
    }

    private string BuildJsonPath(string sourceHost, string path)
    {
        return Path.Combine(path1: this._basePath, path2: sourceHost, path.TrimStart('/'));
    }

    private void EnsureDirectoryExists(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }
        catch (Exception exception)
        {
            this._logger.SaveFailed(filename: folder, message: exception.Message, exception: exception);
        }
    }
}
