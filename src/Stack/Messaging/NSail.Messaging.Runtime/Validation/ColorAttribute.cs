// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace NSail.Messaging.Runtime.Validation;

/// <summary>Refuses text that is not a color a <c>style</c> can safely wear: a hex, an
/// <c>rgb()</c>/<c>rgba()</c> of numbers or a bare keyword, nothing else. An absent or blank
/// value passes: whether a color is required is <see cref="RequiredAttribute"/>'s question.
/// The rule is the one <c>NsColorText</c> paints by.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed partial class ColorAttribute : ValidationAttribute, ICodedValidation
{
    public ColorAttribute()
        : base("The field {0} is not a color.")
    {
    }

    public string Code
    {
        get { return "InvalidFormat"; }
    }

    // None of these shapes can carry a `;`, a quote or a url() into the style attribute.
    [GeneratedRegex(@"^(#[0-9a-fA-F]{3,8}|rgba?\(\s*[0-9.]+%?\s*[, ]\s*[0-9.]+%?\s*[, ]\s*[0-9.]+%?\s*([,/]\s*[0-9.]+%?\s*)?\)|[A-Za-z]{3,30})$")]
    private static partial Regex Pattern();

    /// <summary>Whether the text, trimmed, is a color; blank is not.</summary>
    public static bool IsColor(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) && Pattern().IsMatch(text.Trim());
    }

    public override bool IsValid(object? value)
    {
        return value is not string text || string.IsNullOrWhiteSpace(text) || IsColor(text);
    }
}
