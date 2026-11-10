// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;

namespace NSail.Harness.Hosting;

// The pager every list page shares: newer and older as plain links, the page and the count between.
static class Pager
{
    internal static int Clamp(int? page, int total, int size)
    {
        return Math.Clamp(page ?? 1, 1, Pages(total, size));
    }

    internal static int Pages(int total, int size)
    {
        return Math.Max(1, (total + size - 1) / size);
    }

    internal static void Render(StringBuilder html, int at, int total, int size, string noun, Func<int, string> href, string previous = "‹ Newer", string next = "Older ›")
    {
        var pages = Pages(total, size);
        html.Append("<div class=pager>");
        html.Append(at > 1 ? $"<a href=\"{href(at - 1)}\">{previous}</a>" : "<span></span>");
        html.Append($"<span class=muted>page {at} of {pages} · {total} {noun}{(total == 1 ? "" : "s")}</span>");
        html.Append(at < pages ? $"<a href=\"{href(at + 1)}\">{next}</a>" : "<span></span>");
        html.Append("</div>");
    }
}
