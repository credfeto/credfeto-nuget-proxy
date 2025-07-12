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

    public ApiNugetOrgJsonIndexTransformer(IOptions<ProxyServerConfig> config,
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
            if (resource.Id.StartsWith(uri.CleanUri(), comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                return new(
                    resource.Id.Replace(
                        uri.CleanUri(),
                        this.Config.PublicUrl.CleanUri(),
                        comparisonType: StringComparison.Ordinal
                    ),
                    type: resource.Type,
                    comment: resource.Comment
                );
            }
        }

        return resource;
    }

    public bool IsNuget => true;
}
