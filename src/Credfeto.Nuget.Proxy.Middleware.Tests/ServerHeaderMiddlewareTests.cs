using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FunFair.Test.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Credfeto.Nuget.Proxy.Middleware.Tests;

public sealed class ServerHeaderMiddlewareTests : LoggingFolderCleanupTestBase
{
    public ServerHeaderMiddlewareTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task AddsServerHeaderToEveryResponseAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        using IHost host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services => services.AddSingleton<ServerHeaderMiddleware>());
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<ServerHeaderMiddleware>();
                    app.Run(ctx => ctx.Response.WriteAsync(text: "OK", cancellationToken: ctx.RequestAborted));
                });
            })
            .StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.True(response.Headers.Contains("X-Server"), userMessage: "X-Server response header was not present");
        Assert.Equal(expected: Environment.MachineName, actual: response.Headers.GetValues("X-Server").First());
    }
}
