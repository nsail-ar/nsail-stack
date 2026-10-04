// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Primitives;

namespace NSail.BaseServices.WebApp;

// The browser's ballot, narrowed to what the install offers. The stock provider hands its
// unfiltered header on to the middleware, which resolves it against the SUPPORTED set -- and
// that set carries the base language by construction (every catalog is written in it), so an
// English browser is answered in English at a shop that only ever meant to attend in Spanish.
// Narrowing here rather than by shrinking SupportedCultures keeps a forced choice able to name
// any language the install can render.
sealed class OfferedLanguageProvider : AcceptLanguageHeaderRequestCultureProvider
{
    readonly IReadOnlyCollection<string> _offered;

    public OfferedLanguageProvider(IReadOnlyCollection<string> offered)
    {
        _offered = offered;
    }

    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        // The base parses the header and orders it by quality; what is left is which of those
        // votes this install accepts, in the browser's own order.
        var asked = await base.DetermineProviderCultureResult(httpContext);

        if (asked is null)
        {
            return null;
        }

        List<StringSegment> accepted =
        [
            .. asked.UICultures
                .Select(Accept)
                .OfType<string>()
                .Select(language => new StringSegment(language)),
        ];

        // One list for the words and the numbers both: a culture resolved twice is how a Spanish
        // menu ends up over 580,064.00.
        return accepted.Count > 0 ? new ProviderCultureResult(accepted, accepted) : null;
    }

    // Matched as a language range over the tag, never through CultureInfo: "en-US" is offered by
    // "en", and a header is whatever the caller typed -- a lookup would throw on the first
    // request that asked for a language that does not exist.
    string? Accept(StringSegment asked)
    {
        return _offered.FirstOrDefault(language =>
            asked.Equals(language, StringComparison.OrdinalIgnoreCase)
            || asked.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase));
    }
}
