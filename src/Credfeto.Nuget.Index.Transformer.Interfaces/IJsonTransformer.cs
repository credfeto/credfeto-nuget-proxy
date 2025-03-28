using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Index.Transformer.Interfaces;

public interface IJsonTransformer
{
    ValueTask<JsonResult?> GetFromUpstreamAsync(string path, CancellationToken cancellationToken);
}
