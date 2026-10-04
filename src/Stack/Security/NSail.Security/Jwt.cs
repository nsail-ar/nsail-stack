// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;

namespace NSail.Security;

/// <summary>JWS compact serialization: base64url without padding, and the three-segment
/// split. Not a JWT library — signing and verification stay with the provider that knows
/// which algorithm its vendor uses.</summary>
public static class Jwt
{
    public static string Encode(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string Encode(string value)
    {
        return Encode(Encoding.UTF8.GetBytes(value));
    }

    public static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');

        // Base64 works in quartets; the encoder dropped whatever padding the last one had.
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded,
        };

        return Convert.FromBase64String(padded);
    }

    /// <summary>The token's three segments, or null when it does not have exactly three.</summary>
    public static string[]? Split(string token)
    {
        var parts = token.Split('.');

        return parts.Length == 3 ? parts : null;
    }
}
