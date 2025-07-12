using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Package.Storage.FileSystem.LoggingExtensions;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem;

public sealed class FileSystemPackageStorage : IPackageStorage
{
    private readonly ILogger<FileSystemPackageStorage> _logger;
    private readonly string _basePath;

    public FileSystemPackageStorage(IOptions<ProxyServerConfig> config, ILogger<FileSystemPackageStorage> logger)
    {
        this._logger = logger;

        this._basePath = config.Value.Packages;

        this.EnsureDirectoryExists(this._basePath);
    }

    public async ValueTask<byte[]?> ReadFileAsync(string sourcePath, CancellationToken cancellationToken)
    {
        if (!this.BuildPackagePath(path: sourcePath, out string? packagePath, dir: out _))
        {
            return null;
        }

        try
        {
            if (!File.Exists(packagePath))
            {
                return null;
            }

            return await File.ReadAllBytesAsync(path: packagePath, cancellationToken: cancellationToken);
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
    }

    public async ValueTask SaveFileAsync(string sourcePath, byte[] buffer, CancellationToken cancellationToken)
    {
        if (!this.BuildPackagePath(path: sourcePath, out string? packagePath, out string? dir))
        {
            return;
        }

        try
        {
            this.EnsureDirectoryExists(dir);
            await File.WriteAllBytesAsync(path: packagePath, bytes: buffer, cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            this._logger.SaveFailed(filename: packagePath, message: exception.Message, exception: exception);
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
            this._logger.SaveFailed(filename: folder, message: exception.Message, exception: exception);
        }
    }

    private bool BuildPackagePath(
        string path,
        [NotNullWhen(true)] out string? filename,
        [NotNullWhen(true)] out string? dir
    )
    {
        string f = Path.Combine(path1: this._basePath, path.TrimStart('/'));

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
}
