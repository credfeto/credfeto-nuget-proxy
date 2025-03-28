using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Nuget.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public sealed class StandardJsonIndexTransformer : JsonIndexTransformerBase, IJsonTransformer
{
    public StandardJsonIndexTransformer(
        ProxyServerConfig config,
        IJsonDownloader jsonDownloader,
        ICurrentTimeSource currentTimeSource,
        ILogger<StandardJsonIndexTransformer> logger
    )
        : base(
            config: config,
            jsonDownloader: jsonDownloader,
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

        await this.DoGetFromUpstreamAsync(
            context: context,
            path: path,
            this.ReplaceUrls,
            cancellationToken: context.RequestAborted
        );

        return true;
    }
}
