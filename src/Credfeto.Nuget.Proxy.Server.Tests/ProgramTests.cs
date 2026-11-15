using System.Threading.Tasks;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Nuget.Proxy.Server.Tests;

public sealed class ProgramTests : TestBase
{
    [Fact]
    public async Task SelfTestWithShippedConfigurationPassesAsync()
    {
        int exitCode = await Program.Main(["--self-test"]);

        Assert.Equal(expected: 0, actual: exitCode);
    }
}
