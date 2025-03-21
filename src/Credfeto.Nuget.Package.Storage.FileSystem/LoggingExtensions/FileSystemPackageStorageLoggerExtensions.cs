using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Package.Storage.FileSystem.LoggingExtensions;

internal static partial class FileSystemPackageStorageLoggerExtensions
{
    [LoggerMessage(
        LogLevel.Error,
        EventId = 1,
        Message = "Could not read cached package: {filename}: {message}"
    )]
    public static partial void FailedToReadFileFromCache(
        this ILogger<FileSystemPackageStorage> logger,
        string filename,
        string message,
        Exception exception
    );

    [LoggerMessage(
        LogLevel.Error,
        EventId = 3,
        Message = "Failed to save cached package: {filename}: {message}"
    )]
    public static partial void SaveFailed(
        this ILogger<FileSystemPackageStorage> logger,
        string filename,
        string message,
        Exception exception
    );
}
