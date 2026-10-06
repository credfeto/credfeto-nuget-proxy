using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Server.Helpers.LoggingExtensions;

internal static partial class ServerStartupLoggingExtensions
{
    [LoggerMessage(
        LogLevel.Warning,
        EventId = 1,
        Message = "Proxy:PublicUrl is still the placeholder default {publicUrl}; set it to the public address of this proxy"
    )]
    public static partial void PlaceholderPublicUrl(this ILogger logger, string publicUrl);
}
