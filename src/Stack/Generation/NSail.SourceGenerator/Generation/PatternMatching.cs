// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation;

public static class PatternMatching
{
    public static bool Matches(string fullName, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return true;

        if (string.IsNullOrWhiteSpace(fullName))
            return false;

        var nameParts = fullName.Split('.');
        var patternParts = pattern!.Split('.');

        return MatchParts(nameParts, 0, patternParts, 0);
    }

    static bool MatchParts(ReadOnlySpan<string> name, int nameIndex, ReadOnlySpan<string> pattern, int patternIndex)
    {
        while (true)
        {
            if (patternIndex == pattern.Length && nameIndex == name.Length)
                return true;

            if (patternIndex == pattern.Length)
                return false;

            var token = pattern[patternIndex];

            if (token == "**")
            {
                if (patternIndex + 1 == pattern.Length)
                    return true;

                for (var i = nameIndex; i <= name.Length; i++)
                {
                    if (MatchParts(name, i, pattern, patternIndex + 1))
                        return true;
                }

                return false;
            }

            if (nameIndex == name.Length)
                return false;

            if (token == "*" || token == name[nameIndex])
            {
                nameIndex++;
                patternIndex++;
                continue;
            }

            return false;
        }
    }
}