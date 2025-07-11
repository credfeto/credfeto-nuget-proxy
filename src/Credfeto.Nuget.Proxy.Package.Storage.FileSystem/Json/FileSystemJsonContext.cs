using System.Text.Json.Serialization;
using Credfeto.Nuget.Proxy.Package.Storage.Interfaces.Models;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem.Json;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Serialization | JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    IncludeFields = false
)]
[JsonSerializable(typeof(JsonItem))]
internal sealed partial class FileSystemJsonContext : JsonSerializerContext;
