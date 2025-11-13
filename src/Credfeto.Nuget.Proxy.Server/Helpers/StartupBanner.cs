using System;
using Figgle;

namespace Credfeto.Nuget.Proxy.Server.Helpers;

// https://www.figlet.org/examples.html
[GenerateFiggleText("Banner", "basic", "NuGet Proxy")]
internal static partial class StartupBanner
{
    public static void Show()
    {
        Console.WriteLine(Banner);
        Console.WriteLine();
        Console.WriteLine();        Console.WriteLine("Starting version " + VersionInformation.Version + "...");
    }
}
