using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Credfeto.Nuget.Index.Transformer.Interfaces;

public interface INupkgSource
{
    ValueTask<bool> GetFromUpstreamAsync(
        HttpContext context,
        string path,
        CancellationToken cancellationToken
    );
}
