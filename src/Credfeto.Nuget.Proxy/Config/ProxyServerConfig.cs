using System;
using System.Diagnostics;

namespace Credfeto.Nuget.Proxy.Config;

[DebuggerDisplay("Upstream: {UpstreamUrl} Public: {PublicUrl} Package: {Packages}")]
public sealed record ProxyServerConfig(Uri UpstreamUrl, Uri PublicUrl, string Packages);
