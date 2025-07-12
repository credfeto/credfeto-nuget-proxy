using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;

public interface IJsonTransformer
{
    bool IsNuget { get; }
    
    ValueTask<JsonResult?> GetFromUpstreamAsync(
        string path,
        ProductInfoHeaderValue? userAgent,
        CancellationToken cancellationToken
    );
}
