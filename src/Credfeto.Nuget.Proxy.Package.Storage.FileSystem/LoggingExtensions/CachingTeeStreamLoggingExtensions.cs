using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem.LoggingExtensions;

internal static partial class CachingTeeStreamLoggingExtensions
{
    [LoggerMessage(LogLevel.Error, EventId = 5, Message = "Failed to cache streamed package: {filename}: {message}")]
    public static partial void SaveFailed(this ILogger logger, string filename, string message, Exception exception);

    [LoggerMessage(LogLevel.Warning, EventId = 6, Message = "Failed to delete temp file {filename}: {message}")]
    public static partial void TempFileDeletionFailed(
        this ILogger logger,
        string filename,
        string message,
        Exception exception
    );
}
