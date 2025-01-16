using System;

namespace Credfeto.Nuget.Proxy.Helpers;

internal static class StartupBanner
{
    public static void Show()
    {
        const string banner = @"Nuget Proxy";


        Console.WriteLine(banner);

        Console.WriteLine("Starting version " + VersionInformation.Version + "...");
    }
}