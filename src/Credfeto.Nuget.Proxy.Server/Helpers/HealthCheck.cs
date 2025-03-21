using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Proxy.Server.Helpers;

internal static class HealthCheck
{
    private const int HEALTHCHECK_SUCCESS = 0;
    private const int HEALTHCHECK_FAIL = 1;

    public static async ValueTask<int> ExecuteAsync(string targetUrl)
    {
        if (!Uri.TryCreate(uriString: targetUrl, uriKind: UriKind.Absolute, out Uri? uri))
        {
            return HEALTHCHECK_FAIL;
        }

        using (HttpClient httpClient = new() { BaseAddress = uri })
        {
            httpClient.DefaultRequestHeaders.ConnectionClose = true;

            try
            {
                using (
                    HttpResponseMessage r = await httpClient.GetAsync(
                        requestUri: uri,
                        cancellationToken: CancellationToken.None
                    )
                )
                {
                    return r.IsSuccessStatusCode ? HEALTHCHECK_SUCCESS : HEALTHCHECK_FAIL;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                return HEALTHCHECK_FAIL;
            }
        }
    }

    public static bool IsHealthCheck(string[] args)
    {
        return args.Length == 2
            && StringComparer.OrdinalIgnoreCase.Equals(args[0], y: "--health-check");
    }
}
