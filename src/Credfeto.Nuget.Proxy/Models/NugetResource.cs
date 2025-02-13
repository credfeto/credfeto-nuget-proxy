using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Nuget.Proxy.Models;

[DebuggerDisplay("{Id}: {Type}")]
internal sealed class NugetResource
{
    public NugetResource(string id, string type, string? comment)
    {
        this.Id = id;
        this.Type = type;
        this.Comment = comment;
    }

    [JsonPropertyName("@id")]
    public string Id { get; }

    [JsonPropertyName("@type")]
    public string Type { get; }

    public string? Comment { get; }
}
