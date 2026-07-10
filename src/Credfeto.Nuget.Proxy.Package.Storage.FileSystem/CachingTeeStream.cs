using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Package.Storage.FileSystem.LoggingExtensions;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem;

/// <summary>
///     Reads from <c>source</c> and, as a side effect of each read, writes the same bytes to a temp cache file,
///     renaming it into place once <c>source</c> is fully consumed. Never disposes <c>source</c> - the caller owns
///     that lifetime. A failure writing the cache copy is logged and abandoned; it never prevents bytes reaching
///     the caller.
/// </summary>
internal sealed class CachingTeeStream : Stream
{
    [SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2213: Disposable fields should be disposed",
        Justification = "Owned by the caller (e.g. UpstreamPackageResponse), which disposes it independently"
    )]
    private readonly Stream _source;

    private readonly ILogger _logger;
    private readonly string _tempPath;
    private readonly string _finalPath;
    private FileStream? _tempFileStream;
    private bool _finalized;
    private bool _cachingAbandoned;

    public CachingTeeStream(Stream source, FileStream tempFileStream, string tempPath, string finalPath, ILogger logger)
    {
        this._source = source;
        this._tempFileStream = tempFileStream;
        this._tempPath = tempPath;
        this._finalPath = finalPath;
        this._logger = logger;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await this._source.ReadAsync(buffer, cancellationToken);

        if (read > 0)
        {
            await this.TryWriteToCacheAsync(buffer[..read], cancellationToken);
        }
        else
        {
            await this.TryFinalizeAsync(cancellationToken);
        }

        return read;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Flush()
    {
        // Read-only stream; nothing to flush.
    }

    private async ValueTask TryWriteToCacheAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        if (this._cachingAbandoned || this._tempFileStream is null)
        {
            return;
        }

        try
        {
            await this._tempFileStream.WriteAsync(data, cancellationToken);
        }
        catch (Exception exception)
        {
            this._logger.SaveFailed(filename: this._finalPath, message: exception.Message, exception: exception);
            await this.AbandonCachingAsync();
        }
    }

    private async ValueTask TryFinalizeAsync(CancellationToken cancellationToken)
    {
        if (this._finalized || this._cachingAbandoned || this._tempFileStream is null)
        {
            return;
        }

        try
        {
            await this._tempFileStream.FlushAsync(cancellationToken);
            await this._tempFileStream.DisposeAsync();
            this._tempFileStream = null;
            File.Move(sourceFileName: this._tempPath, destFileName: this._finalPath, overwrite: true);
            this._finalized = true;
        }
        catch (Exception exception)
        {
            this._logger.SaveFailed(filename: this._finalPath, message: exception.Message, exception: exception);
            await this.AbandonCachingAsync();
        }
    }

    private async ValueTask AbandonCachingAsync()
    {
        this._cachingAbandoned = true;

        if (this._tempFileStream is not null)
        {
            await this._tempFileStream.DisposeAsync();
            this._tempFileStream = null;
        }

        try
        {
            File.Delete(this._tempPath);
        }
        catch (Exception exception)
        {
            this._logger.TempFileDeletionFailed(
                filename: this._tempPath,
                message: exception.Message,
                exception: exception
            );
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (!this._finalized && !this._cachingAbandoned)
        {
            await this.AbandonCachingAsync();
        }

        await base.DisposeAsync();
    }
}
