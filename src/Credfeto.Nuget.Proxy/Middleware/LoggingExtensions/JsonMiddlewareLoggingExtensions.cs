using System;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Middleware.LoggingExtensions;

internal static partial class JsonMiddlewareLoggingExtensions
{
    [LoggerMessage(LogLevel.Error, EventId = 1, Message = "Failed to retrieve JSON from {upstream} Received Http {statusCode}")]
    public static partial void UpstreamFailed(this ILogger<JsonMiddleware> logger, Uri upstream, HttpStatusCode statusCode);

    [LoggerMessage(LogLevel.Information, EventId = 2, Message = "Retrieved JSON from {upstream} Received Http {statusCode} Length: {length}")]
    public static partial void UpstreamOk(this ILogger<JsonMiddleware> logger, Uri upstream, HttpStatusCode statusCode, int length);
}