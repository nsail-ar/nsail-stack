// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Cryptography;
using System.Text;

namespace NSail.Data;

/// <summary>A row's id CONSTRUCTED from a name instead of drawn, so two processes writing that
/// row write one row and a restart writes the same one. The name is the caller's whole answer to
/// "which row is this": it carries its own prefix, because the same word under two prefixes must
/// not be the same id.</summary>
public static class DerivedKey
{
    public static Guid For(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(name));

        // Version 8 is RFC 9562's "custom": the bits say the value was constructed rather than
        // drawn, so nothing downstream reads it as a random or a time-ordered id.
        digest[6] = (byte)((digest[6] & 0x0F) | 0x80);
        digest[8] = (byte)((digest[8] & 0x3F) | 0x80);

        return new Guid(digest.AsSpan(0, 16), bigEndian: true);
    }
}
