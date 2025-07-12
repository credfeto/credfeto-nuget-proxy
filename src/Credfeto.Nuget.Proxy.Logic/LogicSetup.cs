using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Logic.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Nuget.Proxy.Logic;

public static class LogicSetup
{
    public static IServiceCollection AddLogic(this IServiceCollection services)
    {
        return services
            .AddJsonClient()
            .AddJsonTransformer()
            .AddSingleton<IJsonDownloader, JsonDownloader>()
            .AddNupkgClient()
            .AddSingleton<INupkgSource, NupkgSource>()
            .AddSingleton<IPackageDownloader, PackageDownloader>();
    }

    private static IServiceCollection AddJsonTransformer(this IServiceCollection services)
    {
        return services
            .AddSingleton<IJsonTransformer, ApiNugetOrgJsonIndexTransformer>()
            .AddSingleton<IJsonTransformer, StandardJsonIndexTransformer>();
    }
}
