using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Credfeto.Nuget.Proxy.Logic.Extensions;

internal static class HttpClientExtensions
{
    public static HttpClient WithBaseAddress(this HttpClient client, Uri baseAddress)
    {
        client.BaseAddress = baseAddress;

        return client;
    }

    public static HttpClient WithUserAgent(this HttpClient client, ProductInfoHeaderValue? userAgent)
    {
        if (userAgent is not null)
        {
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(userAgent);
        }

        return client;
    }
}