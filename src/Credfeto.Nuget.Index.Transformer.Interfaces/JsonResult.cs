using System.Diagnostics;

namespace Credfeto.Nuget.Index.Transformer.Interfaces;

[DebuggerDisplay("{Json}")]
public readonly record struct JsonResult(string Json, int CacheMaxAgeSeconds);
