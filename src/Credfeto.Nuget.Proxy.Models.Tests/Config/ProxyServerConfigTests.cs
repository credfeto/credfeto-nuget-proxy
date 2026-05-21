using Credfeto.Nuget.Proxy.Models.Config;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Nuget.Proxy.Models.Tests.Config;

public sealed class ProxyServerConfigTests : LoggingTestBase
{
    public ProxyServerConfigTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public void DefaultConstructorSetsExpectedValues()
    {
        ProxyServerConfig config = new();

        Assert.Empty(config.UpstreamUrls);
        Assert.Equal(expected: "https://example.com", actual: config.PublicUrl);
        Assert.Equal(expected: "/data/.json", actual: config.Metadata);
        Assert.Equal(expected: "/data/packages", actual: config.Packages);
        Assert.Equal(expected: 60, actual: config.JsonMaxAgeSeconds);
    }

    [Fact]
    public void IsNugetPublicServer_IsTrue_WhenUpstreamContainsNugetOrgUrl()
    {
        ProxyServerConfig config = new() { UpstreamUrls = ["https://api.nuget.org/v3/index.json"] };

        Assert.True(
            config.IsNugetPublicServer,
            userMessage: "Expected IsNugetPublicServer to be true for nuget.org upstream"
        );
    }

    [Fact]
    public void IsNugetPublicServer_IsFalse_WhenUpstreamUrlsIsEmpty()
    {
        ProxyServerConfig config = new() { UpstreamUrls = [] };

        Assert.False(
            config.IsNugetPublicServer,
            userMessage: "Expected IsNugetPublicServer to be false when upstream list is empty"
        );
    }

    [Fact]
    public void IsNugetPublicServer_IsFalse_WhenUpstreamContainsNonNugetUrl()
    {
        ProxyServerConfig config = new() { UpstreamUrls = ["https://example.com"] };

        Assert.False(
            config.IsNugetPublicServer,
            userMessage: "Expected IsNugetPublicServer to be false for non-nuget upstream"
        );
    }

    [Fact]
    public void IsNugetPublicServer_IsFalse_WhenUpstreamContainsInvalidUrl()
    {
        ProxyServerConfig config = new() { UpstreamUrls = ["not-a-valid-url"] };

        Assert.False(
            config.IsNugetPublicServer,
            userMessage: "Expected IsNugetPublicServer to be false for invalid URL"
        );
    }
}
