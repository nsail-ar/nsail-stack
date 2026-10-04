// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Context;

namespace NSail.Messaging.WebApi;

/// <summary>The headers a generated endpoint hands the pipeline as the delivery's own — every
/// header the request carries under the wire prefix, read back under the name the caller sent
/// it with. A request carrying none answers null rather than an empty context, so a hop over
/// HTTP arrives exactly as a headerless in-process send does.</summary>
public static class DeliveryHeaders
{
    public static IReadOnlyDictionary<string, string>? Read(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Dictionary<string, string>? headers = null;

        foreach (var (name, value) in context.Request.Headers)
        {
            if (WireHeaders.Logical(name) is not { } logical)
            {
                continue;
            }

            headers ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // ToString rather than the first value, as the tenancy header beside it is read: a
            // request carrying one name twice renders as one comma-joined word instead of
            // letting a smuggled second copy silently outrank the first.
            headers[logical] = value.ToString();
        }

        return headers;
    }
}
