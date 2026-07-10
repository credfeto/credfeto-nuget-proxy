using System;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Logic.Services;

namespace Credfeto.Nuget.Proxy.Logic;

public interface IPackageDownloader
{
    ValueTask<UpstreamPackageResponse> ReadUpstreamAsync(
        Uri requestUri,
        ProductInfoHeaderValue? userAgent,
        CancellationToken cancellationToken
    );
}
