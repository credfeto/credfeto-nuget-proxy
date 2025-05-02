using System;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Proxy.Logic;

public interface IJsonDownloader
{
    ValueTask<string> ReadUpstreamAsync(Uri requestUri, ProductInfoHeaderValue? userAgent, CancellationToken cancellationToken);
}
