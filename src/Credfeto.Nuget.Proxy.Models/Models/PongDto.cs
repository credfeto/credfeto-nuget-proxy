using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Nuget.Proxy.Models.Models;

[DebuggerDisplay(value: "{Value}")]
public readonly record struct PongDto
{
    [JsonConstructor]
    public PongDto(string value)
    {
        this.Value = value;
    }

    public string Value { get; }
}
