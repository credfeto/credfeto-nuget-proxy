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
        string[] segments,
        out string filename,
        out string dir
    )
    {
        if (segments.Any(ContainsTraversalSegment))
        {
            filename = string.Empty;
            dir = string.Empty;

            return false;
        }

        string[] combineParts = new string[segments.Length + 1];
        combineParts[0] = basePath;

        for (int i = 0; i < segments.Length; ++i)
        {
            combineParts[i + 1] = segments[i].TrimStart('/');
        }

        string full = Path.GetFullPath(Path.Combine(combineParts));

        if (!full.StartsWith(basePathWithSeparator, StringComparison.Ordinal))
        {
            filename = string.Empty;
            dir = string.Empty;

            return false;
        }

        filename = full;

        // ! Path under basePathWithSeparator always produces a path with a directory component
        dir = Path.GetDirectoryName(full)!;

        return true;
    }

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
