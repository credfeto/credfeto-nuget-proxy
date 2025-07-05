using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Docker.HealthCheck.Http.Client;
using Credfeto.Nuget.Proxy.Middleware;
using Credfeto.Nuget.Proxy.Server.Helpers;
using Microsoft.AspNetCore.Builder;

namespace Credfeto.Nuget.Proxy.Server;

public static class Program
{
    private const int MIN_THREADS = 32;

    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0109: Add an overload with a Span or Memory parameter",
        Justification = "Won't work here"
    )]
    public static async Task<int> Main(string[] args)
    {
        return HealthCheckClient.IsHealthCheck(args: args, out string? checkUrl)
            ? await HealthCheckClient.ExecuteAsync(targetUrl: checkUrl, cancellationToken: CancellationToken.None)
            : await RunServerAsync(args);
    }

    private static async ValueTask<int> RunServerAsync(string[] args)
    {
        StartupBanner.Show();

        ServerStartup.SetThreads(MIN_THREADS);

        try
        {
            await using (WebApplication app = ServerStartup.CreateApp(args))
            {
                await RunAsync(app);

                return 0;
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine("An error occurred:");
            Console.WriteLine(exception.Message);
            Console.WriteLine(exception.StackTrace);

            return 1;
        }
    }

    private static Task RunAsync(WebApplication application)
    {
        Console.WriteLine("App Created");

        return AddMiddleware(application).RunAsync();
    }

    private static WebApplication AddMiddleware(WebApplication application)
    {
        return (WebApplication)
            application
                .ConfigureEndpoints()
                .UseMiddleware<JsonMiddleware>()
                .UseMiddleware<NuPkgMiddleware>()
                .UseMiddleware<NotFoundMiddleware>();
    }
}
