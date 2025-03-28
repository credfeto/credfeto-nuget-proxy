using System;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Proxy.Logic;

public interface IJsonDownloader
{
    ValueTask<string> ReadUpstreamAsync(Uri requestUri, CancellationToken cancellationToken);
}
