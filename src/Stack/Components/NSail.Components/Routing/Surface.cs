// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>One of the app's browsing contexts, named: a surface the app draws (aside,
/// modal), one of the reserved link targets (auto, main), or a browser target, which
/// keeps HTML's own '_'-prefixed spelling. It is a type and not a string so a component
/// parameter cannot be handed quoted text — Razor compiles a quoted attribute value as C#
/// for every parameter type except string, which turns a forgotten '@' into a compile
/// error instead of a literal that renders itself.</summary>
public readonly record struct Surface(string Name)
{
    /// <summary>True for a browser target ("_blank"): the link opens a browsing context of
    /// the browser's own, which carries none of this app's surfaces.</summary>
    public bool IsBrowser
    {
        get { return Name.StartsWith('_'); }
    }

    public override string ToString()
    {
        return Name;
    }
}
