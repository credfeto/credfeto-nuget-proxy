using System;
using System.IO;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;

public sealed class PackageResult : IAsyncDisposable
{
    private readonly IAsyncDisposable? _additionalDisposable;

    private PackageResult(
        string? cachedFilePath,
        Stream? upstreamStream,
        long? contentLength,
        IAsyncDisposable? additionalDisposable
    )
    {
        this.CachedFilePath = cachedFilePath;
        this.UpstreamStream = upstreamStream;
        this.ContentLength = contentLength;
        this._additionalDisposable = additionalDisposable;
    }

    public string? CachedFilePath { get; }

    public Stream? UpstreamStream { get; }

    public long? ContentLength { get; }

    public static PackageResult FromCache(string path, long size)
    {
        return new(cachedFilePath: path, upstreamStream: null, contentLength: size, additionalDisposable: null);
    }

    public static PackageResult FromUpstream(
        Stream stream,
        long? contentLength,
        IAsyncDisposable? additionalDisposable = null
    )
    {
        return new(
            cachedFilePath: null,
            upstreamStream: stream,
            contentLength: contentLength,
            additionalDisposable: additionalDisposable
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (this.UpstreamStream is not null)
        {
            await this.UpstreamStream.DisposeAsync();
        }

        if (this._additionalDisposable is not null)
        {
            await this._additionalDisposable.DisposeAsync();
        }
    }
}
