// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;

namespace NSail.Components;

/// <summary>Decides whether text is safe to write into a CSS color position.</summary>
public static partial class NsColorText
{
    // A hex, an rgb()/rgba() of numbers, or a bare keyword: none can carry a `;`, a quote or
    // a url() into the style attribute that wears it.
    [GeneratedRegex(@"^(#[0-9a-fA-F]{3,8}|rgba?\(\s*[0-9.]+%?\s*[, ]\s*[0-9.]+%?\s*[, ]\s*[0-9.]+%?\s*([,/]\s*[0-9.]+%?\s*)?\)|[A-Za-z]{3,30})$")]
    private static partial Regex Color();

    public static bool IsColor(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) && Color().IsMatch(text.Trim());
    }

    public static string? Safe(string? text)
    {
        return IsColor(text) ? text!.Trim() : null;
    }
}
