using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Config;
using Credfeto.Nuget.Proxy.Extensions;
using Credfeto.Nuget.Proxy.Middleware.LoggingExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Middleware;

[SuppressMessage(category: "Microsoft.Security", checkId: "CA3003: Potential Path injection", Justification = "Avoided by checking path above")]
[SuppressMessage(category: "SecurityCodeScan.VS2019", checkId: "SCS0018: Potential Path injection", Justification = "Avoided by checking path above")]
public sealed class NuPkgMiddleware
{
    private readonly ProxyServerConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NuPkgMiddleware> _logger;
    private readonly RequestDelegate _next;

    public NuPkgMiddleware(RequestDelegate next, ProxyServerConfig config, IHttpClientFactory httpClientFactory, ILogger<NuPkgMiddleware> logger)
    {
        this._next = next;
        this._config = config;
        this._httpClientFactory = httpClientFactory;
        this._logger = logger;
    }

    public static string ClientName => "UpStreamJson";

    public async Task InvokeAsync(HttpContext context)
    {
        if (StringComparer.Ordinal.Equals(x: context.Request.Method, y: "GET") && context.Request.Path.HasValue)
        {
            if (context.Request.Path.Value.Contains(value: "../", comparisonType: StringComparison.Ordinal))
            {
                await this._next(context);

                return;
            }

            if (context.Request.Path.Value.EndsWith(value: ".nupkg", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                string packagePath = this.BuildPackagePath(context.Request.Path.Value);

                await this.HandlePackageDownloadAsync(context: context, packagePath: packagePath, cancellationToken: context.RequestAborted);

                return;
            }
        }

        await this._next(context);
    }

    private async Task HandlePackageDownloadAsync(HttpContext context, string packagePath, CancellationToken cancellationToken)
    {
        if (File.Exists(packagePath))
        {
            await this.ServeCachedFileAsync(context: context, packagePath: packagePath, cancellationToken: cancellationToken);

            return;
        }

        await this.GetFromUpstreamAsync(context: context, packagePath: packagePath, cancellationToken: cancellationToken);
    }

    private string BuildPackagePath(string path)
    {
        return Path.Combine(path1: this._config.Packages, path.TrimStart('/'));
    }

    private async Task GetFromUpstreamAsync(HttpContext context, string packagePath, CancellationToken cancellationToken)
    {
        Uri requestUri = this.GetRequestUri(context);
        HttpResponseMessage result = await this.ReadUpstreamAsync(requestUri: requestUri, cancellationToken: cancellationToken);

        if (result.StatusCode != HttpStatusCode.OK)
        {
            await this.UpstreamFailedAsync(context: context, requestUri: requestUri, result: result, cancellationToken: cancellationToken);

            return;
        }

        await using (MemoryStream memoryStream = new())
        {
            await result.Content.CopyToAsync(stream: memoryStream, cancellationToken: cancellationToken);

            OkHeaders(context);

            byte[] buffer = memoryStream.ToArray();
            await context.Response.Body.WriteAsync(buffer: buffer, cancellationToken: cancellationToken);

            this._logger.UpstreamOk(upstream: requestUri, statusCode: result.StatusCode, length: buffer.Length);

            await this.SaveFileAsync(packagePath: packagePath, buffer: buffer, cancellationToken: cancellationToken);
        }
    }

    private async Task SaveFileAsync(string packagePath, byte[] buffer, CancellationToken cancellationToken)
    {
        // ! Doesn't
        string? dir = Path.GetDirectoryName(packagePath);

        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(dir);
            await File.WriteAllBytesAsync(path: packagePath, bytes: buffer, cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            this._logger.SaveFailed(filename: packagePath, message: exception.Message, exception: exception);
        }
    }

    private Task UpstreamFailedAsync(HttpContext context, Uri requestUri, HttpResponseMessage result, CancellationToken cancellationToken)
    {
        this._logger.UpstreamFailed(upstream: requestUri, statusCode: result.StatusCode);
        context.Response.StatusCode = (int)result.StatusCode;

        return result.Content.CopyToAsync(stream: context.Response.Body, cancellationToken: cancellationToken);
    }

    private async Task<HttpResponseMessage> ReadUpstreamAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        HttpClient client = this.GetClient();

        HttpResponseMessage result = await client.GetAsync(requestUri: requestUri, cancellationToken: cancellationToken);

        return result;
    }

    private async Task ServeCachedFileAsync(HttpContext context, string packagePath, CancellationToken cancellationToken)
    {
        Uri requestUri = this.GetRequestUri(context);
        await using (Stream s = File.OpenRead(packagePath))
        {
            OkHeaders(context);
            await s.CopyToAsync(destination: context.Response.Body, cancellationToken: cancellationToken);
            this._logger.Cached(upstream: requestUri, length: s.Position);
        }
    }

    private static void OkHeaders(HttpContext context)
    {
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.Headers.Append(key: "Content-Type", value: "application/octet-stream");
    }

    private Uri GetRequestUri(HttpContext context)
    {
        Uri requestUri = new(this._config.UpstreamUrls[0]
                                 .CleanUri() + context.Request.Path.Value);

        return requestUri;
    }

    private HttpClient GetClient()
    {
        HttpClient client = this._httpClientFactory.CreateClient(ClientName);
        client.BaseAddress = this._config.UpstreamUrls[0];

        return client;
    }
}