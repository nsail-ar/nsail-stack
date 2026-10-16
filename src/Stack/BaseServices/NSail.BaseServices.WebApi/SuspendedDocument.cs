// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text;

namespace NSail.BaseServices.WebApi;

// The page a suspended tenant's browser gets, and it carries its own styling because it has to:
// every asset of this install is an endpoint behind the gate that wrote this, so a document that
// linked a stylesheet would be refused its own stylesheet and arrive bare. That is the point
// rather than a cost — a suspended tenant reaches NOTHING of the app, not its bundle, not its
// brand, not a font, and nothing of this page can fail to render.
//
// It is not NsPageFace's sibling for the same reason: the face is a component of the app, drawn
// by a render that needs the app's assets and a circuit's services, and none of that is reachable
// from here.
static class SuspendedDocument
{
    /// <summary><paramref name="paragraphs"/> is the page's whole body, in order: the words the
    /// pause is explained in, then whatever the row and the install added — when the service
    /// comes back, the writer's own message, who to reach. The page decides none of that, which
    /// is why it takes a list and not a reason.</summary>
    public static string For(string language, string title, IEnumerable<string> paragraphs)
    {
        var builder = new StringBuilder();

        builder.Append("<!DOCTYPE html><html lang=\"").Append(Escaped(language)).Append("\"><head>");
        builder.Append("<meta charset=\"utf-8\">");
        builder.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.Append("<title>").Append(Escaped(title)).Append("</title>");
        builder.Append("<style>");
        builder.Append("html{height:100%}");
        builder.Append("body{margin:0;min-height:100%;display:flex;align-items:center;justify-content:center;");
        builder.Append("background:#f4f5f7;color:#1f2430;");
        builder.Append("font-family:system-ui,-apple-system,\"Segoe UI\",Roboto,Helvetica,Arial,sans-serif}");
        builder.Append("main{max-width:30rem;margin:2rem;padding:2.5rem;border-radius:.75rem;");
        builder.Append("background:#fff;box-shadow:0 1px 3px rgba(0,0,0,.12);text-align:center}");
        builder.Append("h1{margin:0 0 .75rem;font-size:1.5rem;font-weight:600}");
        builder.Append("p{margin:0;font-size:1rem;line-height:1.5;color:#515a6e}");
        builder.Append("p+p{margin-top:.75rem}");
        builder.Append("@media (prefers-color-scheme:dark){");
        builder.Append("body{background:#14161a;color:#e8eaee}");
        builder.Append("main{background:#1e2127;box-shadow:none}");
        builder.Append("p{color:#a8aeba}}");
        builder.Append("</style></head><body><main>");
        builder.Append("<h1>").Append(Escaped(title)).Append("</h1>");

        foreach (var paragraph in paragraphs)
        {
            builder.Append("<p>").Append(Escaped(paragraph)).Append("</p>");
        }

        builder.Append("</main></body></html>");

        return builder.ToString();
    }

    // Most of the words come from the install's own catalogs and its configuration, and the
    // row's message comes from a writer the install trusts — escaped all the same, because text
    // is text and this is markup, and a trusted hand carrying an angle bracket is still the day
    // the page would break.
    static string Escaped(string text)
    {
        return WebUtility.HtmlEncode(text);
    }
}
