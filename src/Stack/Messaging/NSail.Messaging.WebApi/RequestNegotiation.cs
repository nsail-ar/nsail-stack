// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Net.Http.Headers;

namespace NSail.Messaging.WebApi;

public static class RequestNegotiation
{
    static readonly MediaTypeHeaderValue s_document = new("text/html");
    static readonly MediaTypeHeaderValue s_json = new("application/json");

    /// <summary>Whether this caller is a person looking at a page or a program reading an
    /// answer. Accept alone tells the two apart, the negotiation HTTP already has: a document
    /// navigation ranks text/html above everything, while a generated client, a fetch and curl
    /// ask with */* or with nothing at all. So the default wherever the preference is not
    /// explicit is the machine's answer.
    ///
    /// <para>One derivation, asked by everything that owes a caller an answer of its own — the
    /// error handler and the tenant-state gate — because two copies of a content negotiation are
    /// two refusals that look different for no reason anybody chose.</para></summary>
    public static bool PrefersDocument(this HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!MediaTypeHeaderValue.TryParseList(request.Headers.Accept, out var accepted))
        {
            return false;
        }

        var document = -1d;
        var json = -1d;

        foreach (var media in accepted)
        {
            var quality = media.Quality ?? 1d;

            if (s_document.IsSubsetOf(media))
            {
                document = Math.Max(document, quality);
            }

            if (s_json.IsSubsetOf(media))
            {
                json = Math.Max(json, quality);
            }
        }

        return document > json;
    }
}
