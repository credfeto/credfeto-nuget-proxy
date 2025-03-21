using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Nuget.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Middleware.Extensions;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Models.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using AppJsonContexts = Credfeto.Nuget.Proxy.Models.Models.AppJsonContexts;

namespace Credfeto.Nuget.Proxy.Server.Services;

public sealed class ApiNugetOrgJsonIndexTransformer : JsonIndexTransformerBase, IJsonTransformer
{
    private static readonly IReadOnlyList<string> NeededResources =
    [
        "Catalog/3.0.0",
        "PackageBaseAddress/3.0.0",
        "ReadmeUriTemplate/6.13.0",
        "RegistrationsBaseUrl/3.4.0",
        "RepositorySignatures/4.7.0",
        "RepositorySignatures/5.0.0",
        "SearchAutocompleteService/3.0.0-beta",
        "SearchQueryService/3.0.0-beta",
        "VulnerabilityInfo/6.7.0",
    ];

    public ApiNugetOrgJsonIndexTransformer(
        ProxyServerConfig config,
        IHttpClientFactory httpClientFactory,
        ICurrentTimeSource currentTimeSource,
        ILogger<ApiNugetOrgJsonIndexTransformer> logger
    )
        : base(
            config: config,
            httpClientFactory: httpClientFactory,
            currentTimeSource: currentTimeSource,
            logger: logger
        ) { }

    public async ValueTask<bool> GetFromUpstreamAsync(
        HttpContext context,
        string path,
        CancellationToken cancellationToken
    )
    {
        if (!path.EndsWith(value: ".json", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(x: path, y: "/v3/index.json"))
        {
            await this.UpstreamIndexAsync(
                context: context,
                path: path,
                cancellationToken: context.RequestAborted
            );

            return true;
        }

        await this.DoGetFromUpstreamAsync(
            context: context,
            path: path,
            cancellationToken: context.RequestAborted
        );

        return true;
    }

    private async Task UpstreamIndexAsync(
        HttpContext context,
        string path,
        CancellationToken cancellationToken
    )
    {
        Uri requestUri = this.GetRequestUri(path);

        HttpResponseMessage result = await this.ReadUpstreamAsync(
            cancellationToken: cancellationToken,
            requestUri: requestUri
        );

        if (result.StatusCode != HttpStatusCode.OK)
        {
            await this.UpstreamFailedAsync(
                context: context,
                requestUri: requestUri,
                result: result,
                cancellationToken: cancellationToken
            );

            return;
        }

        string json = await result.Content.ReadAsStringAsync(cancellationToken: cancellationToken);

        NugetResources? data = JsonSerializer.Deserialize<NugetResources>(
            json: json,
            jsonTypeInfo: AppJsonContexts.Default.NugetResources
        );

        if (data is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.Headers.CacheControl = $"no-cache, no-store, must-revalidate";

            return;
        }

        NugetResources resources = new(
            version: data.Version,
            [.. data.Resources.Where(IsNeeded).Select(this.RewriteResource)]
        );

        await this.SaveJsonResponseAsync(
            context: context,
            data: resources,
            cancellationToken: cancellationToken
        );
    }

    private static bool IsNeeded(NugetResource resource)
    {
        return NeededResources.Any(n => StringComparer.Ordinal.Equals(x: n, y: resource.Type));
    }

    [SuppressMessage(
        category: "SonarAnalyzer.CSharp",
        checkId: "S3267: Use Linq",
        Justification = "Not Here"
    )]
    private NugetResource RewriteResource(NugetResource resource)
    {
        foreach (Uri uri in this.Config.UpstreamUrls)
        {
            if (
                resource.Id.StartsWith(
                    uri.CleanUri(),
                    comparisonType: StringComparison.OrdinalIgnoreCase
                )
            )
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

    private Task SaveJsonResponseAsync(
        HttpContext context,
        NugetResources data,
        CancellationToken cancellationToken
    )
    {
        string result = JsonSerializer.Serialize(
            value: data,
            jsonTypeInfo: AppJsonContexts.Default.NugetResources
        );

        this.OkHeaders(context);
        return context.Response.WriteAsync(text: result, cancellationToken: cancellationToken);
    }
}
