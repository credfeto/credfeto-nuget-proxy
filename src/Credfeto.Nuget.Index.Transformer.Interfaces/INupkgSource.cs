using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Index.Transformer.Interfaces;

public interface INupkgSource
{
    ValueTask<PackageResult?> GetFromUpstreamAsync(string path, CancellationToken cancellationToken);
}
