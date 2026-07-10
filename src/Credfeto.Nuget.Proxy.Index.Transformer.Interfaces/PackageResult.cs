using System;
using System.IO;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;

public sealed class PackageResult : IAsyncDisposable
{
    private readonly IAsyncDisposable? _additionalDisposable;
    private readonly bool _disposeUpstreamStream;

    private PackageResult(
        string? cachedFilePath,
        Stream? upstreamStream,
        long? contentLength,
        IAsyncDisposable? additionalDisposable,
        bool disposeUpstreamStream
    )
    {
        this.CachedFilePath = cachedFilePath;
        this.UpstreamStream = upstreamStream;
        this.ContentLength = contentLength;
        this._additionalDisposable = additionalDisposable;
        this._disposeUpstreamStream = disposeUpstreamStream;
    }

    public string? CachedFilePath { get; }

    public Stream? UpstreamStream { get; }

    public long? ContentLength { get; }

    public static PackageResult FromCache(string path, long size)
    {
        return new(
            cachedFilePath: path,
            upstreamStream: null,
            contentLength: size,
            additionalDisposable: null,
            disposeUpstreamStream: false
        );
    }

    public static PackageResult FromUpstream(
        Stream stream,
        long? contentLength,
        IAsyncDisposable? additionalDisposable = null,
        bool disposeStream = true
    )
    {
        return new(
            cachedFilePath: null,
            upstreamStream: stream,
            contentLength: contentLength,
            additionalDisposable: additionalDisposable,
            disposeUpstreamStream: disposeStream
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (this._disposeUpstreamStream && this.UpstreamStream is not null)
        {
            await this.UpstreamStream.DisposeAsync();
        }

        if (this._additionalDisposable is not null)
        {
            await this._additionalDisposable.DisposeAsync();
        }
    }
}
