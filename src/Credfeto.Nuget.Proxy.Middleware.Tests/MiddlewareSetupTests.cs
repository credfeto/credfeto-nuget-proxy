using Credfeto.Date.Interfaces;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Models.Config;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Nuget.Proxy.Middleware.Tests;

public sealed class MiddlewareSetupTests : DependencyInjectionTestsBase
{
    public MiddlewareSetupTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services
            .AddMiddleware()
            .AddMockedService<IOptions<ProxyServerConfig>>(static o =>
                o.Value.Returns(new ProxyServerConfig { UpstreamUrls = [] })
            )
            .AddMockedService<ICurrentTimeSource>()
            .AddMockedService<IJsonTransformer>()
            .AddMockedService<INupkgSource>();
    }

    [Fact]
    public void ServerHeaderMiddlewareShouldBeRegistered() => this.RequireService<ServerHeaderMiddleware>();

    [Fact]
    public void JsonMiddlewareShouldBeRegistered() => this.RequireService<JsonMiddleware>();

    [Fact]
    public void NuPkgMiddlewareShouldBeRegistered() => this.RequireService<NuPkgMiddleware>();

    [Fact]
    public void NotFoundMiddlewareShouldBeRegistered() => this.RequireService<NotFoundMiddleware>();
}
