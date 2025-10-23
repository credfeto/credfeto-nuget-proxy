using System;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Logic.Services.LoggingExtensions;

internal static partial class JsonIndexTransformerBaseLoggingExtensions
{
    [LoggerMessage(
        LogLevel.Error,
        EventId = 1,
        Message = "Failed to retrieve JSON from {upstream} Received Http {statusCode}"
    )]
    public static partial void UpstreamJsonFailed(this ILogger logger, Uri upstream, HttpStatusCode statusCode);

    [LoggerMessage(
        LogLevel.Information,
        EventId = 2,
        Message = "Retrieved JSON from {upstream} Received Http {statusCode} Length: {length}"
    )]
    public static partial void UpstreamJsonOk(this ILogger logger, Uri upstream, HttpStatusCode statusCode, int length);

    [LoggerMessage(LogLevel.Information, EventId = 3, Message = "Url Replacement: Replaced {from} with {to}")]
    public static partial void LogUriReplace(this ILogger logger, string from, string to);

    [LoggerMessage(LogLevel.Warning, EventId = 4, Message = "Stripping missing replacement urls")]
    public static partial void StrippingMissingReplacementUrls(this ILogger logger);

    [LoggerMessage(LogLevel.Warning, EventId = 5, Message = "Replacement urls: {urls}")]
    public static partial void ReplacementUrls(this ILogger logger, string urls);

}
