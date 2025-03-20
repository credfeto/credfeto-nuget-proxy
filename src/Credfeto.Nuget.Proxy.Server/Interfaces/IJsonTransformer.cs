using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Credfeto.Nuget.Proxy.Server.Interfaces;

public interface IJsonTransformer
{
    ValueTask<bool> GetFromUpstreamAsync(
        HttpContext context,
        string path,
        CancellationToken cancellationToken
    );
}
