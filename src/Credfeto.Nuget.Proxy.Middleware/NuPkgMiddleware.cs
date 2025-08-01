using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Middleware.Extensions;
using Credfeto.Nuget.Proxy.Middleware.LoggingExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Polly.Bulkhead;
using Polly.Timeout;

namespace Credfeto.Nuget.Proxy.Middleware;

[SuppressMessage(
    category: "Microsoft.Security",
    checkId: "CA3003: Potential Path injection",
    Justification = "Avoided by checking path above"
)]
[SuppressMessage(
    category: "SecurityCodeScan.VS2019",
    checkId: "SCS0018: Potential Path injection",
    Justification = "Avoided by checking path above"
)]
public sealed class NuPkgMiddleware : IMiddleware
{
    private readonly INupkgSource _nupkgSource;
    private readonly ICurrentTimeSource _currentTimeSource;
    private readonly ILogger<NuPkgMiddleware> _logger;

    public NuPkgMiddleware(
        INupkgSource nupkgSource,
        ICurrentTimeSource currentTimeSource,
        ILogger<NuPkgMiddleware> logger
    )
    {
        this._nupkgSource = nupkgSource;
        this._currentTimeSource = currentTimeSource;
        this._logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.GetEndpoint() is not null || !IsMatchingRequest(context: context, out string? path))
        {
            await next(context);

            return;
        }

        CancellationToken cancellationToken = context.RequestAborted;
        ProductInfoHeaderValue? userAgent = context.GetUserAgent();

        try
        {
            PackageResult? result = await this._nupkgSource.GetFromUpstreamAsync(
                path: path,
                userAgent: userAgent,
                cancellationToken: cancellationToken
            );

            if (result is null)
            {
                this._logger.NoContent(path);
                await next(context);
                return;
            }

            this._logger.FoundContent(path: path, length: result.Value.Data.Length);
            await this.SuccessAsync(context: context, data: result.Value, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            HttpStatusCode errorCode = exception.StatusCode ?? HttpStatusCode.InternalServerError;

            this._logger.HttpError(path: path, statusCode: errorCode, message: exception.Message, exception: exception);
            Failed(context: context, result: errorCode);
        }
        catch (JsonException)
        {
            Failed(context: context, result: HttpStatusCode.InternalServerError);
        }
        catch (Exception exception) when (exception is TimeoutRejectedException or BulkheadRejectedException)
        {
            this._logger.TooManyRequests(path: path, message: exception.Message, exception: exception);
            TooManyRequests(context);
        }
        catch (Exception exception)
        {
            this._logger.InternalServerError(path: path, message: exception.Message, exception: exception);
            Failed(context: context, result: HttpStatusCode.InternalServerError);
        }
    }

    private async ValueTask SuccessAsync(HttpContext context, PackageResult data, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.Headers.Append(key: "Content-Type", value: "application/octet-stream");
        context.Response.Headers.CacheControl = "public, max-age=63072000, immutable";
        context.Response.Headers.Expires = this
            ._currentTimeSource.UtcNow()
            .AddSeconds(63072000)
            .ToString(format: "ddd, dd MMM yyyy HH:mm:ss 'GMT'", formatProvider: CultureInfo.InvariantCulture);

        await using (MemoryStream stream = new(data.Data, false))
        {
            await stream.CopyToAsync(context.Response.Body, cancellationToken);
        }
    }

    private static bool IsMatchingRequest(HttpContext context, [NotNullWhen(true)] out string? path)
    {
        if (
            StringComparer.Ordinal.Equals(x: context.Request.Method, y: "GET")
            && context.Request.Path.HasValue
            && !context.Request.Path.Value.Contains(value: "../", comparisonType: StringComparison.Ordinal)
        )
        {
            path = context.Request.Path.Value;

            return true;
        }

        path = null;

        return false;
    }

    private static void Failed(HttpContext context, HttpStatusCode result)
    {
        context.Response.StatusCode = (int)result;
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
    }

    private static void TooManyRequests(HttpContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.Response.Headers.Append(key: "Retry-After", value: "5");
    }
}
