using System;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Logic.Services.LoggingExtensions;

internal static partial class NeedToRenameThisLoggingExtensions
{
    [LoggerMessage(
        LogLevel.Error,
        EventId = 1,
        Message = "Failed to retrieve NUPKG from {upstream} Received Http {statusCode}"
    )]
    public static partial void UpstreamPackageFailed(
        this ILogger<NupkgSource> logger,
        Uri upstream,
        HttpStatusCode statusCode
    );

    [LoggerMessage(
        LogLevel.Information,
        EventId = 2,
        Message = "Retrieved NUPKG from {upstream} Received Http {statusCode} Length: {length}"
    )]
    public static partial void UpstreamPackageOk(
        this ILogger<NupkgSource> logger,
        Uri upstream,
        HttpStatusCode statusCode,
        int length
    );

    [LoggerMessage(
        LogLevel.Information,
        EventId = 2,
        Message = "Retrieved Cached NUPKG from {upstream} Length: {length}"
    )]
    public static partial void CachedPackage(
        this ILogger<NupkgSource> logger,
        Uri upstream,
        long length
    );
}
