using Credfeto.Nuget.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public sealed class StandardJsonIndexTransformer : JsonIndexTransformerBase, IJsonTransformer
{
    public StandardJsonIndexTransformer(
        ProxyServerConfig config,
        IJsonDownloader jsonDownloader,
        ILogger<StandardJsonIndexTransformer> logger
    )
        : base(config: config, jsonDownloader: jsonDownloader, indexReplacement: false, logger: logger) { }
}
