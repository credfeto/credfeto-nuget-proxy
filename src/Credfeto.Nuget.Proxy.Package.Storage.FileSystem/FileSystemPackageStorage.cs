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
    private readonly string _basePathWithSeparator;

    public FileSystemPackageStorage(IOptions<ProxyServerConfig> config, ILogger<FileSystemPackageStorage> logger)
    {
        this._logger = logger;

        (this._basePath, this._basePathWithSeparator) = PathContainment.CreateBase(config.Value.Packages);

        this.EnsureDirectoryExists(this._basePath);
    }

    public async ValueTask<string?> ReadFileAsync(string sourcePath, CancellationToken cancellationToken)
    {
        if (!this.TryBuildPackagePath(path: sourcePath, filename: out string packagePath, dir: out _))
        {
            return null;
        }

        try
        {
            await using (
                FileStream stream = new(
                    path: packagePath,
                    mode: FileMode.Open,
                    access: FileAccess.Read,
                    share: FileShare.Read,
                    bufferSize: 1,
                    useAsync: true
                )
            )
            {
                // Opened purely to validate the file exists and is readable, without buffering its contents.
                _ = stream.Length;
            }

            return packagePath;
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

    [SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2000: Dispose objects before losing scope",
        Justification = "Ownership of the returned tee stream transfers to the caller, who disposes it"
    )]
    public ValueTask<Stream> SaveFileAsync(
        string sourcePath,
        Stream content,
        long? contentLength,
        CancellationToken cancellationToken
    )
    {
        if (!this.TryBuildPackagePath(path: sourcePath, filename: out string packagePath, dir: out string dir))
        {
            return ValueTask.FromResult(content);
        }

        try
        {
            this.EnsureDirectoryExists(dir);
            string tempPath = Path.Combine(dir, Path.GetRandomFileName());

            FileStream tempFileStream = new(
                path: tempPath,
                mode: FileMode.Create,
                access: FileAccess.Write,
                share: FileShare.None,
                bufferSize: 4096,
                useAsync: true
            );

            Stream tee = new CachingTeeStream(
                source: content,
                tempFileStream: tempFileStream,
                tempPath: tempPath,
                finalPath: packagePath,
                logger: this._logger
            );

            return ValueTask.FromResult(tee);
        }
        catch (Exception exception)
        {
            this._logger.SaveFailed(filename: packagePath, message: exception.Message, exception: exception);

            return ValueTask.FromResult(content);
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

    private bool TryBuildPackagePath(string path, out string filename, out string dir)
    {
        return PathContainment.TryBuildContainedPath(
            basePath: this._basePath,
            basePathWithSeparator: this._basePathWithSeparator,
            segment: path,
            filename: out filename,
            dir: out dir
        );
    }
}
