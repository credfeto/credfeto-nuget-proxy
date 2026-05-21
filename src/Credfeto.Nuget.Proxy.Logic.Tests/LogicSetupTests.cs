using System.Net.Http;
using Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Nuget.Proxy.Logic.Tests;

public sealed class LogicSetupTests : DependencyInjectionTestsBase
{
    public LogicSetupTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services
            .AddLogic()
            .AddMockedService<IOptions<ProxyServerConfig>>(static o =>
                o.Value.Returns(new ProxyServerConfig { UpstreamUrls = ["https://api.nuget.org"] })
            )
            .AddMockedService<IJsonStorage>()
            .AddMockedService<IPackageStorage>();
    }

    [Fact]
    public void IJsonDownloaderShouldBeRegistered() => this.RequireService<IJsonDownloader>();

    [Fact]
    public void IPackageDownloaderShouldBeRegistered() => this.RequireService<IPackageDownloader>();

    [Fact]
    public void INupkgSourceShouldBeRegistered() => this.RequireService<INupkgSource>();

    [Fact]
    public void CanCreateJsonHttpClient()
    {
        IHttpClientFactory factory = this.GetService<IHttpClientFactory>();

        using HttpClient client = factory.CreateClient(HttpClientNames.Json);

        Assert.NotNull(client);
    }

    [Fact]
    public void CanCreateNugetPackageHttpClient()
    {
        IHttpClientFactory factory = this.GetService<IHttpClientFactory>();

        using HttpClient client = factory.CreateClient(HttpClientNames.NugetPackage);

        Assert.NotNull(client);
    }
}
