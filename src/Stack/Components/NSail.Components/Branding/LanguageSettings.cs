// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Text.Json.Serialization;

namespace NSail.Components;

/// <summary>What a client needs to decide which language it renders in: the user's own choice,
/// null while nobody has made one, plus the language the install is already serving this
/// request in and the languages it carries.</summary>
public sealed class LanguageSettings
{
    public string? Language { get; set; }

    // Answered, never stored: this type is both the message's result and the shape the settings
    // row serializes, and only the preference belongs in the row. The handler fills these on
    // every read, so they are absent from the JSON a save writes and present in every answer.

    /// <summary>The language this install renders the caller in when nothing is chosen — the
    /// one the request was localized in, not the configured default it usually equals: a client
    /// that recomputed it from configuration would render the prerender it replaces in the
    /// other language whenever a culture cookie moved the server.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Default { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<string>? Supported { get; set; }
}
