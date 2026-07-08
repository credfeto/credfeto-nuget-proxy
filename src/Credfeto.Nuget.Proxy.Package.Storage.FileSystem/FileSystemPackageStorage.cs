using System;
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
        (string packagePath, _) = this.BuildPackagePath(path: sourcePath);

        try
        {
            return await File.ReadAllBytesAsync(path: packagePath, cancellationToken: cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
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
        (string packagePath, string dir) = this.BuildPackagePath(path: sourcePath);

        string? tempPath = null;

        try
        {
            this.EnsureDirectoryExists(dir);
            tempPath = Path.Combine(dir, Path.GetRandomFileName());
            await File.WriteAllBytesAsync(path: tempPath, bytes: buffer, cancellationToken: cancellationToken);
            File.Move(sourceFileName: tempPath, destFileName: packagePath, overwrite: true);
            tempPath = null;
        }
        catch (Exception exception)
        {
            this._logger.SaveFailed(filename: packagePath, message: exception.Message, exception: exception);
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

    private (string filename, string dir) BuildPackagePath(string path)
    {
        string f = Path.Combine(path1: this._basePath, path.TrimStart('/'));

        // ! Path.Combine with an absolute basePath always produces a path with a directory component
        return (f, Path.GetDirectoryName(f)!);
    }
}
