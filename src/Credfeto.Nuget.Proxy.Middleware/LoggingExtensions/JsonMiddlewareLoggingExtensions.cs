using System;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Middleware.LoggingExtensions;

internal static partial class JsonMiddlewareLoggingExtensions
{
    [LoggerMessage(LogLevel.Warning, EventId = 1, Message = "Could not read {path} from upstream.")]
    public static partial void NoUpstreamJson(this ILogger<JsonMiddleware> logger, string path);

    [LoggerMessage(LogLevel.Warning, EventId = 2, Message = "Found {path} from upstream cache for {cacheSeconds}")]
    public static partial void FoundUpstreamJson(this ILogger<JsonMiddleware> logger, string path, long cacheSeconds);

    [LoggerMessage(
        LogLevel.Warning,
        EventId = 3,
        Message = "Could not read {path} from upstream: {statusCode}, {message}."
    )]
    public static partial void HttpError(
        this ILogger<JsonMiddleware> logger,
        string path,
        HttpStatusCode statusCode,
        string message,
        Exception exception
    );

    [LoggerMessage(LogLevel.Warning, EventId = 4, Message = "Invalid json {path} from upstream: {message}.")]
    public static partial void InvalidJson(
        this ILogger<JsonMiddleware> logger,
        string path,
        string message,
        Exception exception
    );

    [LoggerMessage(LogLevel.Warning, EventId = 5, Message = "Too many requests {path} from upstream: {message}.")]
    public static partial void TooManyRequests(
        this ILogger<JsonMiddleware> logger,
        string path,
        string message,
        Exception exception
    );

    [LoggerMessage(LogLevel.Warning, EventId = 6, Message = "Internal server error {path} from upstream: {message}.")]
    public static partial void InternalServerError(
        this ILogger<JsonMiddleware> logger,
        string path,
        string message,
        Exception exception
    );

    [LoggerMessage(LogLevel.Warning, EventId = 7, Message = "Could not read {path} from upstream (404 not found).")]
    public static partial void HttpNotFound(this ILogger<JsonMiddleware> logger, string path);
}
