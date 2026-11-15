using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Server.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Nuget.Proxy.Server.Tests.Helpers;

public sealed class SelfTestTests : LoggingFolderCleanupTestBase
{
    private const string VALID_CONFIG = """
        {
          "Proxy": {
            "UpstreamUrls": [ "https://api.nuget.org" ],
            "PublicUrl": "https://nuget.example.org",
            "Metadata": "metadata",
            "Packages": "packages",
            "JsonMaxAgeSeconds": 60
          }
        }
        """;

    private const string NO_UPSTREAM_CONFIG = """
        {
          "Proxy": {
            "UpstreamUrls": [ ],
            "PublicUrl": "https://nuget.example.org",
            "Metadata": "metadata",
            "Packages": "packages",
            "JsonMaxAgeSeconds": 60
          }
        }
        """;

    private static readonly string[] SelfTestArgs = ["--self-test"];

    public SelfTestTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public void SelfTestSwitchIsDetected()
    {
        Assert.True(SelfTest.IsSelfTest(SelfTestArgs), userMessage: "--self-test should be detected");
    }

    [Fact]
    public void NoArgumentsIsNotSelfTest()
    {
        Assert.False(SelfTest.IsSelfTest([]), userMessage: "No arguments should not be a self-test");
    }

    [Fact]
    public void OtherArgumentsAreNotSelfTest()
    {
        Assert.False(
            SelfTest.IsSelfTest(["--health-check", "http://127.0.0.1:8080/ping", "--self-test-extra"]),
            userMessage: "Other arguments should not be a self-test"
        );
    }

    [Fact]
    public async Task ValidConfigurationPassesAsync()
    {
        await this.WriteConfigAsync(VALID_CONFIG);

        (int exitCode, string output) = await this.RunSelfTestAsync();

        Assert.Equal(expected: 0, actual: exitCode);
        Assert.Equal(expected: "Self-test passed", actual: output);
    }

    [Fact]
    public async Task InvalidConfigurationFailsAsync()
    {
        await this.WriteConfigAsync(NO_UPSTREAM_CONFIG);

        (int exitCode, string output) = await this.RunSelfTestAsync();

        Assert.Equal(expected: 1, actual: exitCode);
        Assert.StartsWith(expectedStartString: "Self-test failed: ", actualString: output, StringComparison.Ordinal);
        Assert.Contains(
            expectedSubstring: "Proxy:UpstreamUrls must contain at least one upstream URL",
            actualString: output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task MissingConfigurationFailsAsync()
    {
        (int exitCode, string output) = await this.RunSelfTestAsync();

        Assert.Equal(expected: 1, actual: exitCode);
        Assert.StartsWith(expectedStartString: "Self-test failed: ", actualString: output, StringComparison.Ordinal);
    }

    private Task WriteConfigAsync(string config)
    {
        CancellationToken cancellationToken = this.CancellationToken();

        return File.WriteAllTextAsync(
            Path.Combine(path1: this.TempFolder, path2: "appsettings.json"),
            contents: config,
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask<(int exitCode, string output)> RunSelfTestAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        await using (StringWriter writer = new())
        {
            int exitCode = await SelfTest.RunAsync(
                args: SelfTestArgs,
                configPath: this.TempFolder,
                output: writer,
                cancellationToken: cancellationToken
            );

            string output = writer.ToString();
            this.Output.WriteLine(output);

            string[] lines = output.Split(
                separator: Environment.NewLine,
                options: StringSplitOptions.RemoveEmptyEntries
            );

            return (exitCode, Assert.Single(lines));
        }
    }
}
