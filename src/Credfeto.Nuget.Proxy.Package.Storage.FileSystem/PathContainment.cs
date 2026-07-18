using System;
using System.IO;
using System.Linq;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem;

internal static class PathContainment
{
    public static (string basePath, string basePathWithSeparator) CreateBase(string configuredPath)
    {
        string basePath = Path.GetFullPath(configuredPath);

        return (basePath, basePath + Path.DirectorySeparatorChar);
    }

    public static bool TryBuildContainedPath(
        string basePath,
        string basePathWithSeparator,
        string segment,
        out string filename,
        out string dir
    )
    {
        if (ContainsTraversalSegment(segment))
        {
            return Fail(filename: out filename, dir: out dir);
        }

        string full = Path.GetFullPath(Path.Combine(basePath, segment.TrimStart('/')));

        return TryFinish(full: full, basePathWithSeparator: basePathWithSeparator, filename: out filename, dir: out dir);
    }

    public static bool TryBuildContainedPath(
        string basePath,
        string basePathWithSeparator,
        string segment1,
        string segment2,
        out string filename,
        out string dir
    )
    {
        if (ContainsTraversalSegment(segment1) || ContainsTraversalSegment(segment2))
        {
            return Fail(filename: out filename, dir: out dir);
        }

        string full = Path.GetFullPath(Path.Combine(basePath, segment1.TrimStart('/'), segment2.TrimStart('/')));

        return TryFinish(full: full, basePathWithSeparator: basePathWithSeparator, filename: out filename, dir: out dir);
    }

    private static bool TryFinish(string full, string basePathWithSeparator, out string filename, out string dir)
    {
        if (!full.StartsWith(basePathWithSeparator, StringComparison.Ordinal))
        {
            return Fail(filename: out filename, dir: out dir);
        }

        filename = full;

        // ! Path under basePathWithSeparator always produces a path with a directory component
        dir = Path.GetDirectoryName(full)!;

        return true;
    }

    private static bool Fail(out string filename, out string dir)
    {
        filename = string.Empty;
        dir = string.Empty;

        return false;
    }

    // Defense-in-depth only: Path.GetFullPath + the base-path prefix check in TryFinish is the
    // actual containment boundary and already rejects any real escape (including prefix-collision
    // siblings). This rejects obviously-malicious segments up front.
    private static bool ContainsTraversalSegment(string path)
    {
        if (path.Contains(value: '\\', comparisonType: StringComparison.Ordinal))
        {
            return true;
        }

        return path.Split('/')
            .Any(segment => string.Equals(a: segment, b: "..", comparisonType: StringComparison.Ordinal));
    }
}
