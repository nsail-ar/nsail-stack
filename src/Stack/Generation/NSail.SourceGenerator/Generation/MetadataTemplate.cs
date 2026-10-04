// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;

namespace NSail.SourceGenerator.Generation;

// Mirror of NSail.Metadata.MetadataProvider's template parsing, duplicated because the
// generator targets netstandard2.0 and cannot reference runtime assemblies — keep the
// binding rules in sync with MetadataProvider.
internal static class MetadataTemplate
{
    const string AttributeName = "NSail.Metadata.MetadataTemplateAttribute";

    static readonly string[] _knownTokens = { "Root", "Area", "Feature", "SubFeature", "Object" };

    public static string? GetDeclaredTemplate(Compilation compilation)
    {
        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == AttributeName)
            {
                return attribute.ConstructorArguments.Length > 0
                    ? attribute.ConstructorArguments[0].Value as string
                    : null;
            }
        }

        return null;
    }

    public static string? GetArea(string fullName, string template)
    {
        if (!TryParse(template, out var left, out var right))
        {
            return null;
        }

        var segments = fullName.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        var boundFromRight = Math.Min(right.Length, segments.Length);

        for (var i = 0; i < boundFromRight; i++)
        {
            if (right[right.Length - 1 - i] == "Area")
            {
                return segments[segments.Length - 1 - i];
            }
        }

        var remaining = segments.Length - boundFromRight;

        for (var i = 0; i < Math.Min(left.Length, remaining); i++)
        {
            if (left[i] == "Area")
            {
                return segments[i];
            }
        }

        return null;
    }

    static bool TryParse(string template, out string[] left, out string[] right)
    {
        var leftTokens = new List<string>();
        var rightTokens = new List<string>();
        var seen = new HashSet<string>();
        var afterStar = false;

        left = [];
        right = [];

        foreach (var part in template.Split('.'))
        {
            if (part == "*")
            {
                if (afterStar)
                {
                    return false;
                }

                afterStar = true;
                continue;
            }

            if (part.Length < 3 || part[0] != '{' || part[part.Length - 1] != '}')
            {
                return false;
            }

            var token = part.Substring(1, part.Length - 2);

            if (!_knownTokens.Contains(token) || !seen.Add(token))
            {
                return false;
            }

            (afterStar ? rightTokens : leftTokens).Add(token);
        }

        left = leftTokens.ToArray();
        right = rightTokens.ToArray();

        return true;
    }
}
