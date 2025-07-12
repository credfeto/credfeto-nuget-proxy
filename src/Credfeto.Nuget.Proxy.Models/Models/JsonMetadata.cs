using System.Diagnostics;

namespace Credfeto.Nuget.Proxy.Models.Models;

[DebuggerDisplay("Etag: {Etag}, Length: {ContentLength} Type: {ContentType}")]
public readonly record struct JsonMetadata(string? Etag, long ContentLength, string? ContentType);
