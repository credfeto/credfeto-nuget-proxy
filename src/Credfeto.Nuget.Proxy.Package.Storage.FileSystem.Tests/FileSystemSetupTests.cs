using System.IO;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem.Tests;

public sealed class FileSystemSetupTests : DependencyInjectionTestsBase
{
    public FileSystemSetupTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services
            .AddFileSystemStorage()
            .AddMockedService<IOptions<ProxyServerConfig>>(static o =>
                o.Value.Returns(new ProxyServerConfig { Metadata = Path.GetTempPath(), Packages = Path.GetTempPath() })
            );
    }

    [Fact]
    public void IJsonStorageShouldBeRegistered() => this.RequireService<IJsonStorage>();

    [Fact]
    public void IPackageStorageShouldBeRegistered() => this.RequireService<IPackageStorage>();
}
