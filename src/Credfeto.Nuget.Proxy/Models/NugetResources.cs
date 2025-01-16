using System.Text.Json.Serialization;

namespace Credfeto.Nuget.Proxy.Models;

internal sealed class NugetResources
{
    [JsonConstructor]
    public NugetResources(string version, NugetResource[] resources)
    {
        this.Version = version;
        this.Resources = resources;
    }

    public string Version { get; }
    public NugetResource[] Resources { get; }
}