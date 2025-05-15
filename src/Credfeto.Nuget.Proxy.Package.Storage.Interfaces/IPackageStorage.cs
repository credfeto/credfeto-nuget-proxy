using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Proxy.Package.Storage.Interfaces;

public interface IPackageStorage
{
    ValueTask<byte[]?> ReadFileAsync(string sourcePath, CancellationToken cancellationToken);

    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0109: Add an overload with a Span or Memory parameter",
        Justification = "Won't work here"
    )]
    ValueTask SaveFileAsync(string sourcePath, byte[] buffer, CancellationToken cancellationToken);
}
