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

        this.EnsureDirectoryExists(config.Packages);
    }

    public async ValueTask<byte[]?> ReadFileAsync(
        string sourcePath,
        CancellationToken cancellationToken
    )
    {
        string packagePath = this.BuildPackagePath(sourcePath);

        try
        {
            if (File.Exists(packagePath))
            {
                return await File.ReadAllBytesAsync(
                    path: packagePath,
                    cancellationToken: cancellationToken
                );
            }
        }
        catch (Exception exception)
        {
            this._logger.FailedToReadFileFromCache(
                filename: sourcePath,
                message: exception.Message,
                exception: exception
            );

            return null;
        }

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
            this.EnsureDirectoryExists(dir);
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
            this._logger.SaveFailed(
                filename: folder,
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
