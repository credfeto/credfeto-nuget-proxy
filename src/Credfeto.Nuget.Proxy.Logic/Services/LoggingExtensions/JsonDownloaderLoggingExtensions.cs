using System;
using System.Net;
using Credfeto.Nuget.Proxy.Logic.Models;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Logic.Services.LoggingExtensions;

internal static partial class JsonDownloaderLoggingExtensions
{
    [LoggerMessage(
        LogLevel.Information,
        EventId = 1,
        Message = "Retrieved {upstream} Metadata : ETag {etag}, Bytes: {contentLength}, Type: {contentType} Http Status {httpStatus}"
    )]
    private static partial void Metadata(
        this ILogger<JsonDownloader> logger,
        Uri upstream,
        string? etag,
        long contentLength,
        string? contentType,
        HttpStatusCode httpStatus
    );

    public static void Metadata(
        this ILogger<JsonDownloader> logger,
        Uri upstream,
        in JsonMetadata metadata,
        HttpStatusCode httpStatus
    )
    {
        logger.Metadata(
            upstream: upstream,
            etag: metadata.Etag,
            contentLength: metadata.ContentLength,
            contentType: metadata.ContentType,
            httpStatus: httpStatus
        );
    }
}
