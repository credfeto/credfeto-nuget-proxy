using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Credfeto.Nuget.Proxy.Models.Models;

[DebuggerDisplay("{Version}")]
public sealed class NugetResources
{
    [JsonConstructor]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0109: Add an overload with a Span or Memory parameter",
        Justification = "Won't work here"
    )]
    public NugetResources(string version, NugetResource[] resources)
    {
        this.Version = version;
        this.Resources = resources;
    }

    public string Version { get; }

    public NugetResource[] Resources { get; }
}
