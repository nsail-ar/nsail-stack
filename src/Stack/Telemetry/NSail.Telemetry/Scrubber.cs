// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using OpenTelemetry;

namespace NSail.Telemetry;

/// <summary>Keeps, on every span the vendor instrumentations produce, only the tags of a closed
/// list; anything else is dropped before export, so a tag a package version adds tomorrow
/// (<c>enduser.id</c>, <c>user_agent.original</c>, a captured header) fails closed. <c>url.full</c>
/// is cut to its origin, and <c>url.path</c> is replaced by the route template
/// (<c>http.route</c>) or dropped where there is none, because a literal path and query carry
/// the ids, documents and search text the route was filled with. NSail's own spans (<c>nsail.*</c>) have nothing to scrub. It is a processor rather
/// than a set of vendor options on purpose: an option renamed by the next package version
/// fails open and silently, a tag that is removed after the fact does not.</summary>
public sealed class Scrubber : BaseProcessor<Activity>
{
    const string UrlTag = "url.full";

    const string PathTag = "url.path";

    const string RouteTag = "http.route";

    const string OwnPrefix = "nsail.";

    static readonly HashSet<string> _allowed = new(StringComparer.Ordinal)
    {
        UrlTag,
        RouteTag,
        "http.request.method",
        "http.response.status_code",
        "url.scheme",
        "server.address",
        "server.port",
        "network.protocol.name",
        "network.protocol.version",
        "error.type",
        "db.system",
        "db.system.name",
        "db.namespace",
        "db.operation.name",
        "db.collection.name",
        "otel.status_code",
        "aspnetcore.components.type",
        "aspnetcore.components.method",
        "aspnetcore.components.circuit.id",
    };

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var route = data.GetTagItem(RouteTag) as string;
        var dropped = new List<string>();

        foreach (var tag in data.TagObjects)
        {
            if (!_allowed.Contains(tag.Key) && !tag.Key.StartsWith(OwnPrefix, StringComparison.Ordinal))
            {
                dropped.Add(tag.Key);
            }
        }

        foreach (var tag in dropped)
        {
            // SetTag(key, null) is how an Activity drops a tag; there is no Remove.
            data.SetTag(tag, null);
        }

        if (!string.IsNullOrEmpty(route))
        {
            data.SetTag(PathTag, route);
        }

        if (data.GetTagItem(UrlTag) is string url)
        {
            data.SetTag(UrlTag, Origin(url));
        }
    }

    static string? Origin(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : null;
    }
}
