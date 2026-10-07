using System;

namespace Credfeto.Nuget.Proxy.Models.Config;

public static class ProxyServerConfigValidation
{
    public const string PlaceholderPublicUrl = "https://example.com";

    public static bool HasUpstreamUrls(ProxyServerConfig config)
    {
        return config.UpstreamUrls.Count != 0;
    }

    public static bool UpstreamUrlsAreAbsolute(ProxyServerConfig config)
    {
        return config.UpstreamUrls.TrueForAll(IsAbsoluteUri);
    }

    public static bool PublicUrlIsAbsolute(ProxyServerConfig config)
    {
        return IsAbsoluteUri(config.PublicUrl);
    }

    public static bool IsPlaceholderPublicUrl(ProxyServerConfig config)
    {
        return StringComparer.Ordinal.Equals(x: config.PublicUrl, y: PlaceholderPublicUrl);
    }

    private static bool IsAbsoluteUri(string value)
    {
        return Uri.TryCreate(uriString: value, uriKind: UriKind.Absolute, out Uri? uri) && IsHttpScheme(uri);
    }

    private static bool IsHttpScheme(Uri uri)
    {
        return StringComparer.Ordinal.Equals(x: uri.Scheme, y: Uri.UriSchemeHttps)
            || StringComparer.Ordinal.Equals(x: uri.Scheme, y: Uri.UriSchemeHttp);
    }
}
