using Credfeto.Nuget.Proxy.Models.Models;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Nuget.Proxy.Models.Tests.Models;

public sealed class PongDtoTests : LoggingTestBase
{
    public PongDtoTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public void ConstructorSetsValue()
    {
        const string EXPECTED = "pong";
        PongDto dto = new(EXPECTED);

        Assert.Equal(expected: EXPECTED, actual: dto.Value);
    }
}
