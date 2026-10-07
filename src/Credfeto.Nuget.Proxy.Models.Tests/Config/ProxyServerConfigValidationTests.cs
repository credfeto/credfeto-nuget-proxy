using Credfeto.Nuget.Proxy.Models.Config;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Nuget.Proxy.Models.Tests.Config;

public sealed class ProxyServerConfigValidationTests : LoggingTestBase
{
    private const string VALID_UPSTREAM = "https://api.nuget.org/v3/index.json";
    private const string VALID_PUBLIC_URL = "https://nuget.example.org";

    public ProxyServerConfigValidationTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public void HasUpstreamUrls_IsFalse_WhenUpstreamUrlsIsEmpty()
    {
        ProxyServerConfig config = new() { UpstreamUrls = [] };

        Assert.False(
            ProxyServerConfigValidation.HasUpstreamUrls(config),
            userMessage: "Expected validation to fail when no upstream URLs are configured"
        );
    }

    [Fact]
    public void HasUpstreamUrls_IsTrue_WhenUpstreamUrlIsConfigured()
    {
        ProxyServerConfig config = new() { UpstreamUrls = [VALID_UPSTREAM] };

        Assert.True(
            ProxyServerConfigValidation.HasUpstreamUrls(config),
            userMessage: "Expected validation to pass when an upstream URL is configured"
        );
    }

    [Fact]
    public void UpstreamUrlsAreAbsolute_IsFalse_WhenUpstreamUrlIsMalformed()
    {
        ProxyServerConfig config = new() { UpstreamUrls = ["not-a-valid-url"] };

        Assert.False(
            ProxyServerConfigValidation.UpstreamUrlsAreAbsolute(config),
            userMessage: "Expected validation to fail for a malformed upstream URL"
        );
    }

    [Fact]
    public void UpstreamUrlsAreAbsolute_IsFalse_WhenAnyUpstreamUrlIsMalformed()
    {
        ProxyServerConfig config = new() { UpstreamUrls = [VALID_UPSTREAM, "not-a-valid-url"] };

        Assert.False(
            ProxyServerConfigValidation.UpstreamUrlsAreAbsolute(config),
            userMessage: "Expected validation to fail when any upstream URL is malformed"
        );
    }

    [Fact]
    public void UpstreamUrlsAreAbsolute_IsTrue_WhenAllUpstreamUrlsAreAbsolute()
    {
        ProxyServerConfig config = new() { UpstreamUrls = [VALID_UPSTREAM, "https://nuget.pkg.github.com/credfeto"] };

        Assert.True(
            ProxyServerConfigValidation.UpstreamUrlsAreAbsolute(config),
            userMessage: "Expected validation to pass when all upstream URLs are absolute"
        );
    }

    [Theory]
    [InlineData("localhost:8080")]
    [InlineData("htps://api.nuget.org/v3/index.json")]
    [InlineData("ftp://api.nuget.org/v3/index.json")]
    [InlineData("/nuget")]
    public static void UpstreamUrlsAreAbsolute_IsFalse_WhenUpstreamUrlIsNotHttpOrHttps(string upstreamUrl)
    {
        ProxyServerConfig config = new() { UpstreamUrls = [upstreamUrl] };

        Assert.False(
            ProxyServerConfigValidation.UpstreamUrlsAreAbsolute(config),
            userMessage: "Expected validation to fail for an upstream URL that is not an absolute http(s) URI"
        );
    }

    [Theory]
    [InlineData("http://nuget.example.org")]
    [InlineData("https://nuget.example.org")]
    public static void PublicUrlIsAbsolute_IsTrue_WhenPublicUrlIsHttpOrHttps(string publicUrl)
    {
        ProxyServerConfig config = new() { UpstreamUrls = [VALID_UPSTREAM], PublicUrl = publicUrl };

        Assert.True(
            ProxyServerConfigValidation.PublicUrlIsAbsolute(config),
            userMessage: "Expected validation to pass for an absolute http(s) public URL"
        );
    }

    [Theory]
    [InlineData("localhost:8080")]
    [InlineData("htps://nuget.example.org")]
    [InlineData("ftp://nuget.example.org")]
    [InlineData("/nuget")]
    public static void PublicUrlIsAbsolute_IsFalse_WhenPublicUrlIsNotHttpOrHttps(string publicUrl)
    {
        ProxyServerConfig config = new() { UpstreamUrls = [VALID_UPSTREAM], PublicUrl = publicUrl };

        Assert.False(
            ProxyServerConfigValidation.PublicUrlIsAbsolute(config),
            userMessage: "Expected validation to fail for a public URL that is not an absolute http(s) URI"
        );
    }

    [Fact]
    public void PublicUrlIsAbsolute_IsFalse_WhenPublicUrlIsMalformed()
    {
        ProxyServerConfig config = new() { UpstreamUrls = [VALID_UPSTREAM], PublicUrl = "not-a-url" };

        Assert.False(
            ProxyServerConfigValidation.PublicUrlIsAbsolute(config),
            userMessage: "Expected validation to fail for a malformed public URL"
        );
    }

    [Fact]
    public void AllRulesPass_WhenConfigIsValid()
    {
        ProxyServerConfig config = new() { UpstreamUrls = [VALID_UPSTREAM], PublicUrl = VALID_PUBLIC_URL };

        Assert.True(
            ProxyServerConfigValidation.HasUpstreamUrls(config),
            userMessage: "Expected upstream URL presence check to pass for a valid config"
        );
        Assert.True(
            ProxyServerConfigValidation.UpstreamUrlsAreAbsolute(config),
            userMessage: "Expected upstream URL format check to pass for a valid config"
        );
        Assert.True(
            ProxyServerConfigValidation.PublicUrlIsAbsolute(config),
            userMessage: "Expected public URL format check to pass for a valid config"
        );
        Assert.False(
            ProxyServerConfigValidation.IsPlaceholderPublicUrl(config),
            userMessage: "Expected a configured public URL not to be treated as the placeholder"
        );
    }

    [Fact]
    public void IsPlaceholderPublicUrl_IsTrue_ForDefaultConfig()
    {
        ProxyServerConfig config = new();

        Assert.True(
            ProxyServerConfigValidation.IsPlaceholderPublicUrl(config),
            userMessage: "Expected the default public URL to be treated as the placeholder"
        );
    }
}
