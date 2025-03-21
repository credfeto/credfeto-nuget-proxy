using Credfeto.Nuget.Proxy.Models.Models;

namespace Credfeto.Nuget.Proxy.Server.Helpers;

internal static class PingPong
{
    public static PongDto Model { get; } = new("Pong!");
}
