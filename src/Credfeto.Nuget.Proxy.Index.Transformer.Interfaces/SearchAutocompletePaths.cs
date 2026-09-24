using System;
using System.Collections.Generic;

namespace Credfeto.Nuget.Proxy.Index.Transformer.Interfaces;

public static class SearchAutocompletePaths
{
    public static readonly IReadOnlySet<string> Paths = new HashSet<string>(
        ["/query", "/autocomplete"],
        StringComparer.OrdinalIgnoreCase
    );
}
