using System;
using System.Net;
using Credfeto.Nuget.Proxy.Logic.Models;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces.Models;
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

    [LoggerMessage(
        LogLevel.Information,
        EventId = 2,
        Message = "Using Cached {upstream} Metadata : ETag {etag}, Bytes: {contentLength}, Type: {contentType} Http Status {httpStatus}"
    )]
    private static partial void ReturningCached(
        this ILogger<JsonDownloader> logger,
        Uri upstream,
        string? etag,
        long contentLength,
        string? contentType,
        HttpStatusCode httpStatus
    );

    [LoggerMessage(LogLevel.Information, EventId = 3, Message = "Previously Cached {upstream} Metadata : ETag {etag}")]
    public static partial void PreviouslyCached(this ILogger<JsonDownloader> logger, Uri upstream, string? etag);

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

    public static void ReturningCached(
        this ILogger<JsonDownloader> logger,
        Uri upstream,
        in JsonItem metadata,
        HttpStatusCode httpStatus
    )
    {
        logger.ReturningCached(
            upstream: upstream,
            etag: metadata.Etag,
            contentLength: metadata.Size,
            contentType: metadata.ContentType,
            httpStatus: httpStatus
        );
    }
}
