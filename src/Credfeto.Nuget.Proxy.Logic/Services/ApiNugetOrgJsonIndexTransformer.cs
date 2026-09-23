using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Extensions;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Models.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NonBlocking;
using AppJsonContexts = Credfeto.Nuget.Proxy.Models.Models.AppJsonContexts;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public sealed class ApiNugetOrgJsonIndexTransformer : JsonIndexTransformerBase, IJsonTransformer
{
    private static readonly IReadOnlyList<string> NeededResources =
    [
        "Catalog/3.0.0",
        "PackageBaseAddress/3.0.0",
        "ReadmeUriTemplate/6.13.0",
        "RegistrationsBaseUrl",
        "RegistrationsBaseUrl/3.4.0",
        "RegistrationsBaseUrl/3.6.0",
        "RegistrationsBaseUrl/Versioned",
        "RepositorySignatures/4.7.0",
        "RepositorySignatures/5.0.0",
        "SearchAutocompleteService/3.0.0-beta",
        "SearchQueryService/3.0.0-beta",
        "VulnerabilityInfo/6.7.0",
    ];

    private static readonly IReadOnlyList<Uri> UpstreamUrl =
    [
        new("https://api.nuget.org"),
        new("https://azuresearch-ussc.nuget.org"),
    ];

    // Seeded with the search/autocomplete paths this transformer whitelists so routing is correct even
    // before /v3/index.json has been fetched once to learn the full mapping below.
    private readonly ConcurrentDictionary<string, Uri> _rewrittenPathUpstreams = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/query"] = UpstreamUrl[1],
        ["/autocomplete"] = UpstreamUrl[1],
    };

    public ApiNugetOrgJsonIndexTransformer(
        IOptions<ProxyServerConfig> config,
        IJsonDownloader jsonDownloader,
        ILogger<ApiNugetOrgJsonIndexTransformer> logger
    )
        : base(config: config, jsonDownloader: jsonDownloader, indexReplacement: true, logger: logger) { }

    protected override async ValueTask<(bool Match, JsonResult? Result)> DoIndexReplacementAsync(
        string path,
        ProductInfoHeaderValue? userAgent,
        CancellationToken cancellationToken
    )
    {
        if (StringComparer.OrdinalIgnoreCase.Equals(x: path, y: "/v3/index.json"))
        {
            JsonResult? result = await this.GetJsonFromUpstreamWithReplacementsAsync(
                path: path,
                queryString: string.Empty,
                userAgent: userAgent,
                transformer: this.ReplaceIndex,
                cancellationToken: cancellationToken
            );

            return (Match: true, Result: result);
        }

        return NoMatch;
    }

    private string ReplaceIndex(string json)
    {
        NugetResources data =
            JsonSerializer.Deserialize<NugetResources>(json: json, jsonTypeInfo: AppJsonContexts.Default.NugetResources)
            ?? throw new JsonException("Invalid json");

        NugetResources resources = new(
            version: data.Version,
            [.. data.Resources.Where(IsNeeded).Select(this.RewriteResource)]
        );

        return JsonSerializer.Serialize(value: resources, jsonTypeInfo: AppJsonContexts.Default.NugetResources);
    }

    private static bool IsNeeded(NugetResource resource)
    {
        return NeededResources.Any(n => StringComparer.Ordinal.Equals(x: n, y: resource.Type));
    }

    [SuppressMessage(category: "SonarAnalyzer.CSharp", checkId: "S3267: Use Linq", Justification = "Not Here")]
    private NugetResource RewriteResource(NugetResource resource)
    {
        foreach (Uri uri in UpstreamUrl)
        {
            string cleanedUpstreamUrl = uri.CleanUri();

            if (resource.Id.StartsWith(cleanedUpstreamUrl, comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                string rewrittenPath = resource.Id[cleanedUpstreamUrl.Length..];
                this._rewrittenPathUpstreams.TryAdd(rewrittenPath, uri);

                return new(
                    new Uri(this.Config.PublicUrl).CleanUri() + rewrittenPath,
                    type: resource.Type,
                    comment: resource.Comment
                );
            }
        }

        return resource;
    }

    protected override Uri GetRequestUri(string path, string queryString)
    {
        // Rewritten search/autocomplete resources can be served from an upstream host (e.g. azuresearch-ussc.nuget.org)
        // other than UpstreamUrls[0]; known paths are seeded above, others are learned as /v3/index.json rewrites them.
        return this._rewrittenPathUpstreams.TryGetValue(path, out Uri? upstream)
            ? new(upstream.CleanUri() + path + queryString)
            : base.GetRequestUri(path: path, queryString: queryString);
    }

    public bool IsNuget => true;
}
