// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace NSail.BaseServices.WebApp;

/// <summary>Forces every HTML document response to revalidate rather than either go stale
/// forever or refuse caching outright. Antiforgery answers a page carrying a fresh token with
/// `no-cache, no-store` (it does not want the token itself cached); a document with no
/// antiforgery interaction on it answers with no Cache-Control header at all, and an
/// intermediary is then free to cache it under its own heuristic — the poisoned-device
/// incident this guards against (stories.md: a stale document keeps naming a retired
/// release's fingerprints and WASM never attaches). Both are wrong the same way: the document
/// must always be asked about, never assumed fresh, but `no-store` throws away the ETag
/// revalidation a 304 could answer with. `no-cache` alone is the middle ground, applied here
/// deterministically to every document response regardless of what ran upstream.</summary>
public static class DocumentCaching
{
    public const string HeaderValue = "no-cache";

    public static void Apply(HttpContext context)
    {
        if (!IsDocument(context.Response))
        {
            return;
        }

        context.Response.Headers.CacheControl = HeaderValue;
        context.Response.Headers.Remove(HeaderNames.Pragma);
        context.Response.Headers.Remove(HeaderNames.Expires);
    }

    // Fingerprinted static assets (/_framework, /_content) carry their own Content-Type —
    // never text/html — so this never has to special-case a route to leave MapStaticAssets'
    // own immutable+max-age header alone.
    static bool IsDocument(HttpResponse response)
    {
        return response.ContentType is { } contentType
            && contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
