using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

namespace Credfeto.Nuget.Proxy.Extensions;

public static class ETagExtensions
{
    private const string WILDCARD = "*";

    public static bool TryNormaliseETag(this string? source, [NotNullWhen(true)] out string? normalised)
    {
        // An empty quoted string is a syntactically valid entity tag, so blank values must be rejected explicitly
        if (string.IsNullOrWhiteSpace(source))
        {
            normalised = null;

            return false;
        }

        // Already a valid strong or weak entity tag (the W/ prefix is preserved); otherwise it is a bare value that
        // needs quoting. A lone wildcard is only meaningful in a request header, never as a resource's own tag.
        if (TryParseTag(source: source, normalised: out normalised))
        {
            return true;
        }

        return TryParseTag(source: "\"" + source + "\"", normalised: out normalised);
    }

    private static bool TryParseTag(string source, [NotNullWhen(true)] out string? normalised)
    {
        if (
            EntityTagHeaderValue.TryParse(input: source, parsedValue: out EntityTagHeaderValue? parsed)
            && !StringComparer.Ordinal.Equals(x: parsed.Tag, y: WILDCARD)
        )
        {
            normalised = parsed.ToString();

            return true;
        }

        normalised = null;

        return false;
    }
}
