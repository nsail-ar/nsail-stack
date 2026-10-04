// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Problems;

public sealed class Issue
{
    public Issue(
        string code,
        string message,
        string? source = null,
        IReadOnlyDictionary<string, string>? arguments = null)
    {
        Code = code;
        Message = message;
        Source = source;
        Arguments = arguments;
    }

    public string Code { get; }

    public string Message { get; }

    public string? Source { get; }

    /// <summary>Values the text is about (a maximum, a bound, an id), by name. The message
    /// carries them already formatted for whoever cannot translate; a translation names them
    /// as tokens ("no more than {max}") and is free to reorder them.</summary>
    public IReadOnlyDictionary<string, string>? Arguments { get; }
}
