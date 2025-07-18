using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem;

public static class FileSystemSetup
{
    public static IServiceCollection AddFileSystemStorage(this IServiceCollection services)
    {
        return services
            .AddSingleton<IJsonStorage, FileSystemJsonStorage>()
            .AddSingleton<IPackageStorage, FileSystemPackageStorage>();
    }
}
