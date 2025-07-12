using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public sealed class StandardJsonIndexTransformer : JsonIndexTransformerBase, IJsonTransformer
{
    public StandardJsonIndexTransformer(
        IOptions<ProxyServerConfig> config,
        IJsonDownloader jsonDownloader,
        ILogger<StandardJsonIndexTransformer> logger
    )
        : base(config: config, jsonDownloader: jsonDownloader, indexReplacement: false, logger: logger) { }

    public bool IsNuget => false;
}
