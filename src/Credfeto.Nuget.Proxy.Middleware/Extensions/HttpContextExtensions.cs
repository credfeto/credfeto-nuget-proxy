using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace Credfeto.Nuget.Proxy.Middleware.Extensions;

internal static class HttpContextExtensions
{
    public static ProductInfoHeaderValue? GetUserAgent(this HttpContext context)
    {
        string? ua = context.Request.Headers.UserAgent;

        if (string.IsNullOrWhiteSpace(ua))
        {
            return null;
        }

        if (ProductInfoHeaderValue.TryParse(ua, out ProductInfoHeaderValue? userAgent))
        {
            return userAgent;
        }

        return null;
    }
}
