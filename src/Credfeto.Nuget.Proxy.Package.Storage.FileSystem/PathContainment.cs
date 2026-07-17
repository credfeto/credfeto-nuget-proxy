using System;
using System.IO;
using System.Linq;

namespace Credfeto.Nuget.Proxy.Package.Storage.FileSystem;

internal static class PathContainment
{
    public static bool ContainsTraversalSegment(string path)
    {
        if (path.Contains(value: '\\', comparisonType: StringComparison.Ordinal))
        {
            return true;
        }

        return path.Split('/')
            .Any(segment => string.Equals(a: segment, b: "..", comparisonType: StringComparison.Ordinal));
    }

    public static string? ResolveWithinBase(string basePathWithSeparator, string combinedPath)
    {
        string full = Path.GetFullPath(combinedPath);

        return full.StartsWith(basePathWithSeparator, StringComparison.Ordinal) ? full : null;
    }
}
