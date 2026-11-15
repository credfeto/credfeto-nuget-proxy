using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Nuget.Proxy.Server.Tests;

public sealed class ProgramTests : TestBase
{
    private const int DEBUG_HTTP_PORT = 9080;
    private const int RELEASE_HTTP_PORT = 8080;

    [Fact]
    public async Task SelfTestWithShippedConfigurationPassesAsync()
    {
        int exitCode = await Program.Main(["--self-test"]);

        Assert.Equal(expected: 0, actual: exitCode);
    }

    [Fact]
    public async Task ServerFailsWhenHttpPortIsAlreadyInUseAsync()
    {
        using (TcpListener debugPort = OccupyPort(DEBUG_HTTP_PORT))
        {
            using (TcpListener releasePort = OccupyPort(RELEASE_HTTP_PORT))
            {
                int exitCode = await Program.Main([]);

                Assert.Equal(expected: 1, actual: exitCode);
            }
        }
    }

    private static TcpListener OccupyPort(int port)
    {
        TcpListener listener = new(localaddr: IPAddress.Any, port: port);

        try
        {
            listener.Start();

            return listener;
        }
        catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            // Another process already holds the port, which blocks the server just as well.
            return listener;
        }
    }
}
