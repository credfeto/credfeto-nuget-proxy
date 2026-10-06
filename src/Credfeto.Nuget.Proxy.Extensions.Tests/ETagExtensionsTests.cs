using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Nuget.Proxy.Extensions.Tests;

public sealed class ETagExtensionsTests : LoggingTestBase
{
    public ETagExtensionsTests(ITestOutputHelper output)
        : base(output) { }

    [Theory]
    [InlineData("W/\"abc\"", "W/\"abc\"")]
    [InlineData("abc", "\"abc\"")]
    [InlineData("\"abc\"", "\"abc\"")]
    [InlineData("*", "\"*\"")]
    public static void TryNormaliseETag_ReturnsNormalisedValue(string input, string expected)
    {
        bool success = input.TryNormaliseETag(out string? normalised);

        Assert.True(condition: success, userMessage: "Should have normalised the ETag");
        Assert.Equal(expected: expected, actual: normalised);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab\"c")]
    public static void TryNormaliseETag_RejectsInvalidValue(string? input)
    {
        bool success = input.TryNormaliseETag(out string? normalised);

        Assert.False(condition: success, userMessage: "Should not have normalised the ETag");
        Assert.Null(normalised);
    }
}
