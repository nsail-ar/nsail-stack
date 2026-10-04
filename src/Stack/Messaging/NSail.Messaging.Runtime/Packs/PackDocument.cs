// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using System.Text.RegularExpressions;
using NSail.Serialization;

namespace NSail.Messaging.Runtime.Packs;

/// <summary>What is done to a pack's text before it is a Pack: every guid in the document is
/// answered for the importing scope, and only then is it read. The seam is total because the
/// document cannot say which of its ids it means — one it names, one it creates, one it points
/// back at — and it lives here so every owner of a pack applies the same law
/// (TenancyProvider.Owned is the usual answer).</summary>
public static partial class PackDocument
{
    public static string Resolve(string body, Func<Guid, Guid> owned)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(owned);

        return Guids().Replace(body, match => owned(Guid.Parse(match.ValueSpan)).ToString());
    }

    public static Pack Parse(string body, string name)
    {
        return JsonSerializer.Deserialize<Pack>(body, JsonOptions.Wire)
            ?? throw new InvalidOperationException($"Pack '{name}' deserialized to null.");
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex Guids();
}
