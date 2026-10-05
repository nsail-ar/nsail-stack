// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

// What a browser sends: every event carries the whole box, not the one character that arrived.
// A single Input() of the finished text is a PASTE, and a handler that runs once per character
// is right about a paste and wrong about typing — which is why a suite about the keystroke has
// to spell the prefixes (testing.md).
static class Typing
{
    public static IEnumerable<string> Prefixes(string text)
    {
        for (var length = 1; length <= text.Length; length++)
        {
            yield return text[..length];
        }
    }
}
