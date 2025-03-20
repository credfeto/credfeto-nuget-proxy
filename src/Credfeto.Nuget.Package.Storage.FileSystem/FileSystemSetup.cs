using Credfeto.Nuget.Package.Storage.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Nuget.Package.Storage.FileSystem;

public static class FileSystemSetup
{
    public static IServiceCollection AddFileSystemStorage(this IServiceCollection services)
    {
        return services.AddSingleton<IPackageStorage, FileSystemPackageStorage>();
    }
}
