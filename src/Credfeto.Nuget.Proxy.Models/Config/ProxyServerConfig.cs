using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Credfeto.Nuget.Proxy.Models.Config;

[DebuggerDisplay("Upstream: {UpstreamUrls.Count} Public: {PublicUrl} Package: {Packages}")]
public sealed class ProxyServerConfig
{
    public ProxyServerConfig()
    {
        this.UpstreamUrls = [];
        this.PublicUrl = "https://example.com";
        this.Metadata = "/data/.json";
        this.Packages = "/data/packages";
        this.JsonMaxAgeSeconds = 60;
    }

    public List<string> UpstreamUrls { get; set; }

    [SuppressMessage(
        category: "Microsoft.Design",
        checkId: "CA1056: Should ne a uri",
        Justification = "Not for config"
    )]
    public string PublicUrl { get; set; }

    public string Metadata { get; set; }

    public string Packages { get; set; }

    public int JsonMaxAgeSeconds { get; set; }

    public bool IsNugetPublicServer => this.UpstreamUrls.Exists(IsNuget);

    private static bool IsNuget(string h)
    {
        return Uri.TryCreate(uriString: h, uriKind: UriKind.Absolute, out Uri? uri)
            && uri.DnsSafeHost.EndsWith(value: ".nuget.org", comparisonType: StringComparison.OrdinalIgnoreCase);
    }
}
