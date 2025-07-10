using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces.Models;

namespace Credfeto.Nuget.Proxy.Package.Storage.Interfaces;

public interface IJsonStorage
{
    ValueTask<JsonItem?> LoadAsync(Uri requestUri, CancellationToken cancellationToken);

    ValueTask SaveAsync(Uri requestUri, JsonItem item, CancellationToken cancellationToken);
}