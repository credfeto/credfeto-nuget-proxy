using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Proxy.Logic;

public interface IJsonDownloader
{
    ValueTask<HttpResponseMessage> ReadUpstreamAsync(
        Uri requestUri,
        CancellationToken cancellationToken
    );
}
