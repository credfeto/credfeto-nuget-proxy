using Microsoft.AspNetCore.Builder;

namespace Credfeto.Nuget.Proxy.Server.Helpers;

internal static partial class Endpoints
{
    public static WebApplication ConfigureEndpoints(this WebApplication app)
    {
        return app.ConfigureTestEndpoints();
    }
}
