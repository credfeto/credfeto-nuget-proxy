using System;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Middleware.LoggingExtensions;

internal static partial class NuPkgMiddlewareLoggingExtensions
{
    [LoggerMessage(LogLevel.Warning, EventId = 1, Message = "Could not read {path} from upstream.")]
    public static partial void NoContent(this ILogger<NuPkgMiddleware> logger, string path);

    [LoggerMessage(LogLevel.Warning, EventId = 2, Message = "Found {path} from upstream of {length} bytes.")]
    public static partial void FoundContent(this ILogger<NuPkgMiddleware> logger, string path, long length);

    [LoggerMessage(LogLevel.Warning, EventId = 3, Message = "Could not read {path} from upstream: {statusCode}, {message}.")]
    public static partial void HttpError(this ILogger<NuPkgMiddleware> logger, string path, HttpStatusCode statusCode, string message, Exception exception);

    [LoggerMessage(LogLevel.Warning, EventId = 4, Message = "Invalid json {path} from upstream: {message}.")]
    public static partial void InvalidJson(this ILogger<NuPkgMiddleware> logger, string path, string message, Exception exception);

    [LoggerMessage(LogLevel.Warning, EventId = 5, Message = "Too many requests {path} from upstream: {message}.")]
    public static partial void TooManyRequests(this ILogger<NuPkgMiddleware> logger, string path, string message, Exception exception);

    [LoggerMessage(LogLevel.Warning, EventId = 6, Message = "Internal server error {path} from upstream: {message}.")]
    public static partial void InternalServerError(this ILogger<NuPkgMiddleware> logger, string path, string message, Exception exception);
}