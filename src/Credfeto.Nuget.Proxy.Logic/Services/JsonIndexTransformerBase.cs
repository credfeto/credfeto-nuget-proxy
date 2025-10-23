using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Extensions;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Logic.Services.LoggingExtensions;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public abstract class JsonIndexTransformerBase
{
    private readonly IJsonDownloader _jsonDownloader;
    private readonly bool _indexReplacement;
    private readonly ILogger _logger;

    protected JsonIndexTransformerBase(
        IOptions<ProxyServerConfig> config,
        IJsonDownloader jsonDownloader,
        bool indexReplacement,
        ILogger logger
    )
    {
        this.Config = config.Value;
        this._jsonDownloader = jsonDownloader;
        this._indexReplacement = indexReplacement;
        this._logger = logger;
    }

    protected ProxyServerConfig Config { get; }

    public async ValueTask<JsonResult?> GetFromUpstreamAsync(
        string path,
        ProductInfoHeaderValue? userAgent,
        CancellationToken cancellationToken
    )
    {
        if (this._indexReplacement)
        {
            (bool match, JsonResult? result) = await this.DoIndexReplacementAsync(
                path,
                userAgent: userAgent,
                cancellationToken
            );

            if (match)
            {
                return result;
            }
        }

        return await this.GetJsonFromUpstreamWithReplacementsAsync(
            path: path,
            userAgent: userAgent,
            transformer: this.ReplaceUrls,
            cancellationToken: cancellationToken
        );
    }

    [SuppressMessage(
        "Roslynator.Analyzers",
        "RCS1231: Make ref read-only",
        Justification = "Derived classes need it without in"
    )]
    protected virtual ValueTask<(bool Match, JsonResult? Result)> DoIndexReplacementAsync(
        string path,
        ProductInfoHeaderValue? userAgent,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(NoMatch);
    }

    protected static (bool Match, JsonResult? Result) NoMatch { get; } = (Match: false, Result: null);

    protected async ValueTask<JsonResult?> GetJsonFromUpstreamWithReplacementsAsync(
        string path,
        ProductInfoHeaderValue? userAgent,
        Func<string, string> transformer,
        CancellationToken cancellationToken
    )
    {
        Uri requestUri = this.GetRequestUri(path);

        JsonResponse response = await this._jsonDownloader.ReadUpstreamAsync(
            requestUri: requestUri,
            userAgent: userAgent,
            cancellationToken: cancellationToken
        );

        string json = transformer(response.Json);

        this._logger.UpstreamJsonOk(upstream: requestUri, statusCode: HttpStatusCode.OK, length: json.Length);

        return new(Json: json, this.GetJsonCacheMaxAge(path), ETag: response.ETag);
    }

    protected Uri GetRequestUri(string path)
    {
        return new(new Uri(this.Config.UpstreamUrls[0]).CleanUri() + path);
    }

    protected string ReplaceUrls(string json)
    {


        return this.Config.UpstreamUrls.Aggregate(seed: json, func: this.ReplaceOneUrl);
    }

    private string ReplaceOneUrl(string current, string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            this._logger.StrippingMissingReplacementUrls();
            this._logger.ReplacementUrls(this.BuildReplacementUrlsStatus());

            return current;
        }

        string from = CleanUpstreamUrl(uri);
        string to = this.CleanPublicUri();

        string result = current.Replace(from, to, comparisonType: StringComparison.Ordinal);

        if (!StringComparer.Ordinal.Equals(result, current))
        {
            this._logger.LogUriReplace(from, to);
        }

        return result;
    }

    private string BuildReplacementUrlsStatus()
    {
        StringBuilder builder = new();

        for (int index = 0; index < this.Config.UpstreamUrls.Count; ++index)
        {
            builder = builder.Append(index)
                             .Append(" ==> (")
                             .Append((object?)this.Config.UpstreamUrls[index])
                             .Append(')')
                             .Append(' ');
        }

        return builder.ToString()
                      .TrimEnd(' ');
    }

    private static string CleanUpstreamUrl(string uri)
    {
        return new Uri(uri).CleanUri();
    }

    private string CleanPublicUri()
    {
        return new Uri(this.Config.PublicUrl).CleanUri();
    }

    private int GetJsonCacheMaxAge(string path)
    {
        return path.StartsWith("/v3/vulnerabilties/", comparisonType: StringComparison.OrdinalIgnoreCase)
            ? this.Config.JsonMaxAgeSeconds * 10
            : this.Config.JsonMaxAgeSeconds;
    }
}
