using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Models.Models;

namespace Credfeto.Nuget.Proxy.Package.Storage.Interfaces;

public interface IJsonStorage
{
    ValueTask<(JsonMetadata metadata, string content)?> LoadAsync(Uri requestUri, CancellationToken cancellationToken);

    ValueTask SaveAsync(Uri requestUri, JsonMetadata metadata, string jsonContent, CancellationToken cancellationToken);
}