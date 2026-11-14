// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Validation;

namespace NSail.Components;

/// <summary>Decides whether text is safe to write into a CSS color position, by the rule
/// <see cref="ColorAttribute"/> refuses a message's color with.</summary>
public static class NsColorText
{
    public static bool IsColor(string? text)
    {
        return ColorAttribute.IsColor(text);
    }

    public static string? Safe(string? text)
    {
        return IsColor(text) ? text!.Trim() : null;
    }
}
