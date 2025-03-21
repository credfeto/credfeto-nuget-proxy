using System;

namespace Credfeto.Nuget.Proxy.Middleware.Extensions;

public static class UriExtensions
{
    public static string CleanUri(this Uri uri)
    {
        return uri.ToString().TrimEnd('/');
    }
}
