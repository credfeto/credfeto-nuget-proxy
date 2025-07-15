using System.Diagnostics;

namespace Credfeto.Nuget.Proxy.Logic;

[DebuggerDisplay("Etag: {ETag} - Json: {Json}")]
public readonly record struct JsonResponse(string Json, string ETag);