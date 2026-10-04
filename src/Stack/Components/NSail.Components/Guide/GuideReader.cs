// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using System.Globalization;
using System.Net.Http;

namespace NSail.Components;

/// <summary>Fetches a chapter's markdown from the static assets its module ships, in the UI
/// culture where that module wrote one and in GuideItem.DefaultCulture where it did not. Every
/// chapter read stays read for the rest of the scope, so paging back and forth through a guide
/// costs one request per chapter and no more.</summary>
public class GuideReader(HttpClient client, NavigationManager navigation)
{
    readonly Dictionary<string, GuideChapter?> _read = new(StringComparer.Ordinal);

    public virtual async Task<GuideChapter?> Read(GuideItem chapter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        if (_read.TryGetValue(chapter.File, out var known))
        {
            return known;
        }

        foreach (var culture in Cultures())
        {
            if (await Fetch(chapter, culture, cancellationToken) is not { } body)
            {
                continue;
            }

            var read = new GuideChapter(body, chapter.Folder(culture));

            _read[chapter.File] = read;

            return read;
        }

        _read[chapter.File] = null;

        return null;
    }

    async Task<string?> Fetch(GuideItem chapter, string culture, CancellationToken cancellationToken)
    {
        // Absolute, because the client the factory hands out carries no base address and a
        // browser cannot send a relative one — where the app is served from is the navigation
        // manager's answer, on either host.
        var address = new Uri(new Uri(navigation.BaseUri), chapter.Path(culture));

        using var response = await client.GetAsync(address, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    static IEnumerable<string> Cultures()
    {
        var current = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        yield return current;

        if (!string.Equals(current, GuideItem.DefaultCulture, StringComparison.OrdinalIgnoreCase))
        {
            yield return GuideItem.DefaultCulture;
        }
    }
}
