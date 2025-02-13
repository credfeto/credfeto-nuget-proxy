using System.Text.Json.Serialization;

namespace Credfeto.Nuget.Proxy.Models;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Serialization | JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    IncludeFields = false
)]
[JsonSerializable(typeof(NugetResources))]
[JsonSerializable(typeof(NugetResource))]
internal sealed partial class AppJsonContexts : JsonSerializerContext;
