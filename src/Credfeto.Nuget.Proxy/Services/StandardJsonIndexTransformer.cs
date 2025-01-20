using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Nuget.Proxy.Config;
using Credfeto.Nuget.Proxy.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Services;

public sealed class StandardJsonIndexTransformer : JsonIndexTransformerBase, IJsonTransformer
{
    public StandardJsonIndexTransformer(ProxyServerConfig config, IHttpClientFactory httpClientFactory, ICurrentTimeSource currentTimeSource, ILogger<StandardJsonIndexTransformer> logger)
        : base(config: config, httpClientFactory: httpClientFactory, currentTimeSource: currentTimeSource, logger: logger)
    {
    }

    public async ValueTask<bool> GetFromUpstreamAsync(HttpContext context, string path, CancellationToken cancellationToken)
    {
        if (!path.EndsWith(value: ".json", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        await this.DoGetFromUpstreamAsync(context: context, path: path, cancellationToken: context.RequestAborted);

        return true;
    }
}