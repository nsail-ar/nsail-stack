// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace NSail.BaseServices.WebApp;

/// <summary>Makes every HTML document response revalidate instead of going stale: a stale
/// document keeps naming a retired release's fingerprints and WASM never attaches. An anonymous
/// document gets `no-cache`, which keeps ETag revalidation. A response already saying `no-store`
/// (antiforgery's token-bearing page) is left alone, and an authenticated document answers
/// `private, no-store` because it carries personal data that must not reach a disk cache.</summary>
public static class DocumentCaching
{
    public const string HeaderValue = "no-cache";

    public const string AuthenticatedHeaderValue = "private, no-store";

    public static void Apply(HttpContext context)
    {
        if (!IsDocument(context.Response))
        {
            return;
        }

        if (context.Response.Headers.CacheControl.ToString().Contains("no-store", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            context.Response.Headers.CacheControl = AuthenticatedHeaderValue;
            context.Response.Headers.Remove(HeaderNames.Pragma);
            context.Response.Headers.Remove(HeaderNames.Expires);

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
