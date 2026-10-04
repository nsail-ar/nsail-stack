// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using OpenTelemetry;

namespace NSail.Telemetry;

/// <summary>Drops, from every span the vendor instrumentations produce, the four tags that
/// can carry a person: the query string a list screen's search box arrives in (url.query on
/// the server span, and the whole of it again inside url.full on the calling client's), the
/// SQL text, and any captured query parameter. NSail's own spans have nothing to scrub —
/// they never read a message's members — so this exists for the code NSail did not write,
/// and it is a processor rather than a set of vendor options on purpose: an option renamed
/// by the next package version fails open and silently, a tag that is removed after the
/// fact does not.</summary>
public sealed class Scrubber : BaseProcessor<Activity>
{
    const string UrlTag = "url.full";

    const string ParameterPrefix = "db.query.parameter.";

    static readonly string[] _dropped = ["url.query", "db.statement", "db.query.text"];

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        foreach (var tag in _dropped)
        {
            // SetTag(key, null) is how an Activity drops a tag; there is no Remove.
            data.SetTag(tag, null);
        }

        foreach (var parameter in Parameters(data))
        {
            data.SetTag(parameter, null);
        }

        if (data.GetTagItem(UrlTag) is string url && Trim(url) is { } trimmed)
        {
            data.SetTag(UrlTag, trimmed);
        }
    }

    static List<string> Parameters(Activity data)
    {
        var found = new List<string>();

        foreach (var tag in data.TagObjects)
        {
            if (tag.Key.StartsWith(ParameterPrefix, StringComparison.Ordinal))
            {
                found.Add(tag.Key);
            }
        }

        return found;
    }

    static string? Trim(string url)
    {
        var mark = url.IndexOf('?', StringComparison.Ordinal);

        return mark < 0 ? null : url[..mark];
    }
}
