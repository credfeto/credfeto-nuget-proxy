using System;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Nuget.Proxy.Extensions.Tests;

public sealed class UriExtensionsTests : LoggingTestBase
{
    public UriExtensionsTests(ITestOutputHelper output)
        : base(output) { }

    [Theory]
    [InlineData("https://example.com/path/", "https://example.com/path")]
    [InlineData("https://example.com/path", "https://example.com/path")]
    [InlineData("https://example.com/", "https://example.com")]
    [InlineData("https://example.com", "https://example.com")]
    public void CleanUri_StripsTrailingSlash(string input, string expected)
    {
        string result = new Uri(input).CleanUri();

        Assert.Equal(expected: expected, actual: result);
    }
}
