using System.Text.Json.Serialization;

namespace Credfeto.Nuget.Proxy.Models;

internal sealed class NugetResource
{
    public NugetResource(string id, string type)
    {
        this.Id = id;
        this.Type = type;
    }

    [JsonPropertyName("@id")]
    public string Id { get; }
    [JsonPropertyName("@type")]
    public string Type { get; }
}