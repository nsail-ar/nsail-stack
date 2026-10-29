// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace NSail.Storage.S3;

// AWS Signature Version 4, written here rather than taken from a vendor SDK: the whole of what
// an S3-compatible store asks of a caller is this signature, and every S3-compatible service
// speaks it — pulling a vendor's client in would tie the kit to one of them to avoid ninety
// lines of HMAC.
static class S3Signature
{
    public const string Algorithm = "AWS4-HMAC-SHA256";

    // What a PUT declares instead of a hash, so the body is read once. Only legitimate over
    // TLS, which is why S3StorageProvider hashes the payload instead when the administrator's
    // own endpoint is not https.
    public const string UnsignedPayload = "UNSIGNED-PAYLOAD";

    public const string EmptyPayload = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    // Only these are signed. content-length is deliberately out: proxies rewrite it, and AWS's
    // own clients leave it unsigned for that reason.
    static bool IsSigned(string header)
    {
        return header is "host" or "content-type" or "range"
            || header.StartsWith("x-amz-", StringComparison.Ordinal);
    }

    public static void Apply(
        HttpRequestMessage request,
        string accessKey,
        string secretKey,
        string region,
        string payloadHash,
        DateTimeOffset now)
    {
        var stamp = now.UtcDateTime.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.TryAddWithoutValidation("x-amz-date", stamp);

        request.Headers.TryAddWithoutValidation("Authorization", Authorization(request, accessKey, secretKey, region, payloadHash, now));
    }

    public static string Authorization(
        HttpRequestMessage request,
        string accessKey,
        string secretKey,
        string region,
        string payloadHash,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);

        var uri = request.RequestUri!;
        var date = now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var stamp = now.UtcDateTime.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var scope = $"{date}/{region}/s3/aws4_request";

        var headers = Headers(request, uri, payloadHash, stamp);
        var signed = string.Join(';', headers.Keys);

        var canonical = string.Join('\n',
            request.Method.Method,
            uri.AbsolutePath.Length == 0 ? "/" : uri.AbsolutePath,
            CanonicalQuery(uri),
            string.Concat(headers.Select(header => $"{header.Key}:{header.Value}\n")),
            signed,
            payloadHash);

        var toSign = string.Join('\n', Algorithm, stamp, scope, Hex(Sha256(Encoding.UTF8.GetBytes(canonical))));

        var key = Hmac(Hmac(Hmac(Hmac(Encoding.UTF8.GetBytes($"AWS4{secretKey}"), date), region), "s3"), "aws4_request");

        return $"{Algorithm} Credential={accessKey}/{scope}, SignedHeaders={signed}, Signature={Hex(Hmac(key, toSign))}";
    }

    // The two headers the signature always covers are seeded here rather than read back off the
    // request, so the same call answers whether or not Apply has put them there yet — which is
    // what lets a test check this against a published vector without building a live request.
    static SortedDictionary<string, string> Headers(HttpRequestMessage request, Uri uri, string payloadHash, string stamp)
    {
        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}",
            ["x-amz-content-sha256"] = payloadHash,
            ["x-amz-date"] = stamp,
        };

        foreach (var header in request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>()))
        {
            var name = header.Key.ToLowerInvariant();

            if (IsSigned(name))
            {
                headers[name] = string.Join(",", header.Value.Select(value => value.Trim()));
            }
        }

        return headers;
    }

    static string CanonicalQuery(Uri uri)
    {
        if (uri.Query.Length <= 1)
        {
            return "";
        }

        var pairs = uri.Query[1..]
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Select(parts => (Name: Escape(Unescape(parts[0])), Value: parts.Length > 1 ? Escape(Unescape(parts[1])) : ""))
            .OrderBy(pair => pair.Name, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal);

        return string.Join('&', pairs.Select(pair => $"{pair.Name}={pair.Value}"));
    }

    static string Unescape(string value)
    {
        return Uri.UnescapeDataString(value.Replace('+', ' '));
    }

    // EscapeDataString is RFC 3986 exactly — unreserved is A-Za-z0-9-._~ and nothing else —
    // which is the encoding the canonical request is defined in.
    static string Escape(string value)
    {
        return Uri.EscapeDataString(value);
    }

    static byte[] Sha256(byte[] value)
    {
        return System.Security.Cryptography.SHA256.HashData(value);
    }

    static byte[] Hmac(byte[] key, string value)
    {
        return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value));
    }

    static string Hex(byte[] value)
    {
        return Convert.ToHexStringLower(value);
    }
}
