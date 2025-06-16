using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Nuget.Proxy.Middleware;

public static class MiddlewareSetup
{
    public static IServiceCollection AddMiddleware(this IServiceCollection services)
    {
        return services.AddSingleton<JsonMiddleware>()
                       .AddSingleton<NuPkgMiddleware>()
                       .AddSingleton<NotFoundMiddleware>();
    }
}