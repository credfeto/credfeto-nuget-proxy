using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces.Models;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem;

public sealed class FileSystemJsonStorage :  IJsonStorage
{
    public ValueTask<JsonItem?> LoadAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<JsonItem?>(null);
    }

    public ValueTask SaveAsync(Uri requestUri, JsonItem item, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}