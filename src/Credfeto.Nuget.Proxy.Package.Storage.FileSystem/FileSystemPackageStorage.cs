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

        this._basePath = Path.GetFullPath(config.Value.Packages);
        this._basePathWithSeparator = this._basePath + Path.DirectorySeparatorChar;

        this.EnsureDirectoryExists(this._basePath);
    }

    public async ValueTask<string?> ReadFileAsync(string sourcePath, CancellationToken cancellationToken)
    {
        (string filename, string dir)? built = this.BuildPackagePath(path: sourcePath);

        if (built is null)
        {
            return null;
        }

        string packagePath = built.Value.filename;

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
        (string filename, string dir)? built = this.BuildPackagePath(path: sourcePath);

        if (built is null)
        {
            return ValueTask.FromResult(content);
        }

        (string packagePath, string dir) = built.Value;

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

    private (string filename, string dir)? BuildPackagePath(string path)
    {
        if (PathContainment.ContainsTraversalSegment(path))
        {
            return null;
        }

        string? f = PathContainment.ResolveWithinBase(
            basePathWithSeparator: this._basePathWithSeparator,
            combinedPath: Path.Combine(path1: this._basePath, path.TrimStart('/'))
        );

        if (f is null)
        {
            return null;
        }

        // ! Path under _basePathWithSeparator always produces a path with a directory component
        return (f, Path.GetDirectoryName(f)!);
    }
}
