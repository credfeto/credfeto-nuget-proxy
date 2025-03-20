using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Credfeto.Nuget.Proxy.Models.Config;

[DebuggerDisplay("Upstream: {UpstreamUrls.Count} Public: {PublicUrl} Package: {Packages}")]
public sealed record ProxyServerConfig(
    IReadOnlyList<Uri> UpstreamUrls,
    Uri PublicUrl,
    string Packages
)
{
    public bool IsNugetPublicServer =>
        this.UpstreamUrls.Any(h =>
            h.DnsSafeHost.EndsWith(
                value: ".nuget.org",
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        );
}
