using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Package.Storage.FileSystem.LoggingExtensions;
using Credfeto.Nuget.Package.Storage.Interfaces;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Package.Storage.FileSystem;

public sealed class FileSystemPackageStorage : IPackageStorage
{
    private readonly ProxyServerConfig _config;
    private readonly ILogger<FileSystemPackageStorage> _logger;

    public FileSystemPackageStorage(
        ProxyServerConfig config,
        ILogger<FileSystemPackageStorage> logger
    )
    {
        this._config = config;
        this._logger = logger;

        Directory.CreateDirectory(config.Packages);
    }

    public async ValueTask<Stream?> ReadFileAsync(
        string sourcePath,
        CancellationToken cancellationToken
    )
    {
        string packagePath = this.BuildPackagePath(sourcePath);

        if (File.Exists(packagePath))
        {
            try
            {
                return File.OpenRead(packagePath);
            }
            catch (Exception exception)
            {
                this._logger.FailedToReadFileFromCache(sourcePath, exception.Message, exception);

                return null;
            }
        }

        await Task.CompletedTask;
        return null;
    }

    public async ValueTask SaveFileAsync(
        string sourcePath,
        byte[] buffer,
        CancellationToken cancellationToken
    )
    {
        string packagePath = this.BuildPackagePath(sourcePath);

        // ! Doesn't
        string? dir = Path.GetDirectoryName(packagePath);

        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(dir);
            await File.WriteAllBytesAsync(
                path: packagePath,
                bytes: buffer,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception)
        {
            this._logger.SaveFailed(
                filename: packagePath,
                message: exception.Message,
                exception: exception
            );
        }
    }

    private string BuildPackagePath(string path)
    {
        return Path.Combine(path1: this._config.Packages, path.TrimStart('/'));
    }
}
