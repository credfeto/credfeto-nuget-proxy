using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Index.Transformer.Interfaces;

public interface IJsonTransformer
{
    ValueTask<JsonResult?> GetFromUpstreamAsync(string path, ProductInfoHeaderValue? userAgent, CancellationToken cancellationToken);
}
