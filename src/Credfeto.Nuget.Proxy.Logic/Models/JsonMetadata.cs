using System.Diagnostics;

namespace Credfeto.Nuget.Proxy.Logic.Models;

[DebuggerDisplay("Etag: {Etag}, Length: {ContentLength} Type: {ContentType}")]
internal readonly record struct JsonMetadata(string? Etag, long ContentLength, string? ContentType);