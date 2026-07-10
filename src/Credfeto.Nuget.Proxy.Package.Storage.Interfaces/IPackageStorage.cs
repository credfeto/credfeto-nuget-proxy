using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Proxy.Package.Storage.Interfaces;

public interface IPackageStorage
{
    ValueTask<string?> ReadFileAsync(string sourcePath, CancellationToken cancellationToken);

    ValueTask<Stream> SaveFileAsync(
        string sourcePath,
        Stream content,
        long? contentLength,
        CancellationToken cancellationToken
    );
}
