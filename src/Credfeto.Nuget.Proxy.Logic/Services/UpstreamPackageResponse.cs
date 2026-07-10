using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Proxy.Logic.Services;

public sealed class UpstreamPackageResponse : IAsyncDisposable
{
    private readonly HttpResponseMessage _response;

    private UpstreamPackageResponse(HttpResponseMessage response, Stream content, long? contentLength)
    {
        this._response = response;
        this.Content = content;
        this.ContentLength = contentLength;
    }

    public Stream Content { get; }

    public long? ContentLength { get; }

    public static async ValueTask<UpstreamPackageResponse> CreateAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);

        return new(response: response, content: content, contentLength: response.Content.Headers.ContentLength);
    }

    public async ValueTask DisposeAsync()
    {
        await this.Content.DisposeAsync();
        this._response.Dispose();
    }
}
