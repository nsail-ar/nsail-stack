// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Context;

/// <summary>The headers one delivery was published or sent with — the operation's own
/// context, reached through <see cref="MessageContextAccessor"/> and never through an
/// ambient anyone may write. Values are strings only, so a header crosses HTTP with no
/// serialization anybody had to invent.</summary>
public sealed class MessageContext
{
    // Copied rather than held: the caller's dictionary is theirs to keep mutating, and a
    // header read after the call would then answer with something the operation was never
    // published with. Case-insensitive because the same names travel as HTTP headers.
    public MessageContext(IReadOnlyDictionary<string, string>? headers)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (headers is not null)
        {
            foreach (var (name, value) in headers)
            {
                values[name] = value;
            }
        }

        Headers = values;
    }

    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>The value this delivery carries under <paramref name="name"/>, null when it
    /// carries none.</summary>
    public string? GetHeader(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return Headers.TryGetValue(name, out var value) ? value : null;
    }
}
