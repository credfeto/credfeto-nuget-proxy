using Credfeto.Nuget.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Logic.Services;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Nuget.Proxy.Logic;

public static class LogicSetup
{
    public static IServiceCollection AddLogic(this IServiceCollection services, ProxyServerConfig appConfig)
    {
        return services
            .AddJsonClient(appConfig)
            .AddJsonTransformer(appConfig)
            .AddSingleton<IJsonDownloader, JsonDownloader>()
            .AddNupkgClient(appConfig)
            .AddSingleton<INupkgSource, NupkgSource>()
            .AddSingleton<IPackageDownloader, PackageDownloader>();
    }

    private static IServiceCollection AddJsonTransformer(this IServiceCollection services, ProxyServerConfig appConfig)
    {
        return appConfig.IsNugetPublicServer
            ? services.AddSingleton<IJsonTransformer, ApiNugetOrgJsonIndexTransformer>()
            : services.AddSingleton<IJsonTransformer, StandardJsonIndexTransformer>();
    }
}
