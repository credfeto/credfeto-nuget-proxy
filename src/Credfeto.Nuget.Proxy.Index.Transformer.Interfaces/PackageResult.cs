using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;

[SuppressMessage(
    category: "Meziantou.Analyzer",
    checkId: "MA0109: Add an overload with a Span or Memory parameter",
    Justification = "Won't work here"
)]
[DebuggerDisplay("Length: {Data.Length} bytes")]
public readonly record struct PackageResult(byte[] Data);
