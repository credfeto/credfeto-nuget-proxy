using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem.LoggingExtensions;

internal static partial class FileSystemJsonStorageLoggerExtensions
{
    [LoggerMessage(LogLevel.Error, EventId = 1, Message = "Could not read cached json: {filename}: {message}")]
    public static partial void FailedToReadFileFromCache(
        this ILogger<FileSystemJsonStorage> logger,
        string filename,
        string message,
        Exception exception
    );

    [LoggerMessage(LogLevel.Error, EventId = 3, Message = "Failed to save cached json: {filename}: {message}")]
    public static partial void SaveFailed(
        this ILogger<FileSystemJsonStorage> logger,
        string filename,
        string message,
        Exception exception
    );

    [LoggerMessage(LogLevel.Warning, EventId = 4, Message = "Failed to delete temp file {filename}: {message}")]
    public static partial void TempFileDeletionFailed(
        this ILogger<FileSystemJsonStorage> logger,
        string filename,
        string message,
        Exception exception
    );

    [LoggerMessage(
        LogLevel.Warning,
        EventId = 5,
        Message = "Failed to delete corrupt cache file {filename}: {message}"
    )]
    public static partial void CorruptFileDeletionFailed(
        this ILogger<FileSystemJsonStorage> logger,
        string filename,
        string message,
        Exception exception
    );
}
