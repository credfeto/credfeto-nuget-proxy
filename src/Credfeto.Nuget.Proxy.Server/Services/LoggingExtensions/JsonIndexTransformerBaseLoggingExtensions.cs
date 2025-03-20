using System;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Server.Services.LoggingExtensions;

internal static partial class JsonIndexTransformerBaseLoggingExtensions
{
    [LoggerMessage(
        LogLevel.Error,
        EventId = 1,
        Message = "Failed to retrieve JSON from {upstream} Received Http {statusCode}"
    )]
    public static partial void UpstreamFailed(
        this ILogger logger,
        Uri upstream,
        HttpStatusCode statusCode
    );

    [LoggerMessage(
        LogLevel.Information,
        EventId = 2,
        Message = "Retrieved JSON from {upstream} Received Http {statusCode} Length: {length}"
    )]
    public static partial void UpstreamOk(
        this ILogger logger,
        Uri upstream,
        HttpStatusCode statusCode,
        int length
    );
}
