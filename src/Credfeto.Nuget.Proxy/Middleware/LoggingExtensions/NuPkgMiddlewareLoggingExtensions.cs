using System;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Middleware.LoggingExtensions;

internal static partial class NuPkgMiddlewareLoggingExtensions
{
    [LoggerMessage(
        LogLevel.Error,
        EventId = 1,
        Message = "Failed to retrieve NUPKG from {upstream} Received Http {statusCode}"
    )]
    public static partial void UpstreamFailed(
        this ILogger<NuPkgMiddleware> logger,
        Uri upstream,
        HttpStatusCode statusCode
    );

    [LoggerMessage(
        LogLevel.Information,
        EventId = 2,
        Message = "Retrieved NUPKG from {upstream} Received Http {statusCode} Length: {length}"
    )]
    public static partial void UpstreamOk(
        this ILogger<NuPkgMiddleware> logger,
        Uri upstream,
        HttpStatusCode statusCode,
        int length
    );

    [LoggerMessage(
        LogLevel.Information,
        EventId = 2,
        Message = "Retrieved Cached NUPKG from {upstream} Length: {length}"
    )]
    public static partial void Cached(
        this ILogger<NuPkgMiddleware> logger,
        Uri upstream,
        long length
    );
}
