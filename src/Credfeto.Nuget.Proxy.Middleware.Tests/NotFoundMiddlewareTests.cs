using System;
using System.Net;
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

public sealed class NotFoundMiddlewareTests : LoggingTestBase
{
    public NotFoundMiddlewareTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task InvokeAsync_CallsNext_WhenEndpointIsSetAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        using IHost host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services => services.AddSingleton<NotFoundMiddleware>().AddRouting());
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseMiddleware<NotFoundMiddleware>();
                    app.UseEndpoints(endpoints =>
                        endpoints.MapGet(
                            "/ping",
                            async ctx =>
                                await ctx.Response.WriteAsync(text: "pong", cancellationToken: ctx.RequestAborted)
                        )
                    );
                });
            })
            .StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/ping", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns405_WhenNoEndpointIsSetAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        using IHost host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services => services.AddSingleton<NotFoundMiddleware>());
                webBuilder.Configure(app => app.UseMiddleware<NotFoundMiddleware>());
            })
            .StartAsync(cancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: new Uri(uriString: "/any-path", UriKind.Relative),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: HttpStatusCode.MethodNotAllowed, actual: response.StatusCode);
    }
}
