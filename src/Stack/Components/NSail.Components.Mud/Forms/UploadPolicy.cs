// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;

namespace NSail.Components;

/// <summary>What an upload field lets in: the type is decided from the file's extension and its
/// leading bytes against an allow-list, never from what the client claims. Types that run script
/// when opened (svg, html) are only admitted when <c>Accept</c> names them outright, never
/// through a wildcard.</summary>
static class UploadPolicy
{
    sealed record Kind(string ContentType, byte[]? Signature, int Offset = 0, bool Active = false);

    static readonly Dictionary<string, Kind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = new("image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        [".jpg"] = new("image/jpeg", [0xFF, 0xD8, 0xFF]),
        [".jpeg"] = new("image/jpeg", [0xFF, 0xD8, 0xFF]),
        [".gif"] = new("image/gif", "GIF8"u8.ToArray()),
        [".webp"] = new("image/webp", "WEBP"u8.ToArray(), 8),
        [".pdf"] = new("application/pdf", "%PDF"u8.ToArray()),
        [".svg"] = new("image/svg+xml", null, Active: true),
        [".txt"] = new("text/plain", null),
        [".csv"] = new("text/csv", null),
        [".json"] = new("application/json", null),
        [".xml"] = new("application/xml", null, Active: true),
        [".html"] = new("text/html", null, Active: true),
        [".htm"] = new("text/html", null, Active: true),
        [".docx"] = new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50, 0x4B]),
        [".xlsx"] = new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [0x50, 0x4B]),
        [".zip"] = new("application/zip", [0x50, 0x4B]),
    };

    /// <summary>The content type to store for this file, or null when <paramref name="accept"/>
    /// does not admit it. With no accept list the client's type is kept as given.</summary>
    public static string? Resolve(string? accept, string name, string clientType, byte[] head)
    {
        if (string.IsNullOrWhiteSpace(accept))
        {
            return clientType;
        }

        var extension = Path.GetExtension(name);

        var tokens = accept.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!Kinds.TryGetValue(extension, out var kind))
        {
            return tokens.Any(token => AdmitsUnlisted(token, extension, clientType)) ? Inert : null;
        }

        if (!Matches(kind.Signature, kind.Offset, head))
        {
            return null;
        }

        return tokens.Any(token => Admits(token, extension, kind)) ? kind.ContentType : null;
    }

    const string Inert = "application/octet-stream";

    // An extension the allow-list does not know is admitted only when Accept names it or the
    // client's claimed type fits a token; it is stored as inert bytes, never as the claim.
    static bool AdmitsUnlisted(string token, string extension, string clientType)
    {
        if (token.StartsWith('.'))
        {
            return string.Equals(token, extension, StringComparison.OrdinalIgnoreCase);
        }

        if (token == "*/*")
        {
            return true;
        }

        var claim = new Kind(clientType, null, Active: IsActive(clientType));

        return Admits(token, extension, claim);
    }

    static bool IsActive(string contentType)
    {
        return contentType.Contains("html", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("xml", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("script", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>How many leading bytes <see cref="Resolve"/> needs to see.</summary>
    public const int HeadLength = 16;

    /// <summary>A size in the unit a person reads: bytes, KB or MB, one decimal at most.</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return Format(bytes / (1024d * 1024d)) + " MB";
        }

        if (bytes >= 1024)
        {
            return Format(bytes / 1024d) + " KB";
        }

        return bytes.ToString(CultureInfo.InvariantCulture) + " B";
    }

    static string Format(double value)
    {
        return (Math.Floor(value * 10) / 10).ToString("0.#", CultureInfo.InvariantCulture);
    }

    static bool Admits(string token, string extension, Kind kind)
    {
        if (token.StartsWith('.'))
        {
            return string.Equals(token, extension, StringComparison.OrdinalIgnoreCase);
        }

        if (token == "*/*")
        {
            return !kind.Active;
        }

        if (token.EndsWith("/*", StringComparison.Ordinal))
        {
            return !kind.Active && kind.ContentType.StartsWith(token[..^1], StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(token, kind.ContentType, StringComparison.OrdinalIgnoreCase);
    }

    static bool Matches(byte[]? signature, int offset, byte[] head)
    {
        return signature is null
            || (head.Length >= offset + signature.Length && head.AsSpan(offset, signature.Length).SequenceEqual(signature));
    }
}
