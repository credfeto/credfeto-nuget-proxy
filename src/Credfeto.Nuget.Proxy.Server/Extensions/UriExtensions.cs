using System;

namespace Credfeto.Nuget.Proxy.Server.Extensions;

internal static class UriExtensions
{
    public static string CleanUri(this Uri uri)
    {
        return uri.ToString().TrimEnd('/');
    }
}
