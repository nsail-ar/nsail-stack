// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace NSail.Text;

/// <summary>Renders a closed subset of markdown — headings (1-6), blockquote, ordered and
/// unordered lists, paragraphs, inline bold/italic and a link — to HTML or to text, plus the
/// images and pipe tables a MarkdownOptions admits. Nothing else: code fences and everything
/// an option did not open are not recognized and pass through as literal text. Source text is
/// HTML-encoded before any tag is added, so the input never needs to be trusted, and a link or
/// an image whose scheme is not one of the few allowed stays literal text.</summary>
public static class Markdown
{
    static readonly Regex Heading = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
    static readonly Regex OrderedItem = new(@"^\d+\.\s+(.*)$", RegexOptions.Compiled);
    static readonly Regex UnorderedItem = new(@"^[-*]\s+(.*)$", RegexOptions.Compiled);
    // Both emphases in one pattern, and matched in one pass: two passes let the shorter one
    // read the delimiters the longer one just wrote, so bold rendered as "*loud*" comes back
    // out as italic.
    static readonly Regex Emphasis = new(@"\*\*(.+?)\*\*|\*(.+?)\*", RegexOptions.Compiled);
    static readonly Regex Link = new(@"\[([^\]]+)\]\(([^)\s]+)\)", RegexOptions.Compiled);
    static readonly Regex Image = new(@"!\[([^\]]*)\]\(([^)\s]+)\)", RegexOptions.Compiled);
    // A row of at least two cells, and a rule whose cells are dashes with an optional
    // alignment colon at either end — the two lines together are what makes a table, so a
    // paragraph that happens to start with a pipe is still a paragraph.
    static readonly Regex TableRow = new(@"^\s*\|(.+)\|\s*$", RegexOptions.Compiled);
    static readonly Regex TableRule = new(@"^\s*\|(\s*:?-{1,}:?\s*\|)+\s*$", RegexOptions.Compiled);

    static readonly string[] Schemes = ["http://", "https://", "mailto:"];
    static readonly string[] ImageSchemes = ["http://", "https://"];

    public static string ToHtml(string? markdown)
    {
        return ToHtml(markdown, null);
    }

    public static string ToHtml(string? markdown, MarkdownOptions? options)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        options ??= MarkdownOptions.Prose;

        var html = new StringBuilder();

        foreach (var block in Read(markdown, options))
        {
            AppendHtml(html, block, options);
        }

        return html.ToString();
    }

    /// <summary>The document's headings in the order they appear, each with the id the rendered
    /// heading carries — what an index is built from, so the rail and the anchors it points at
    /// cannot drift.</summary>
    public static IReadOnlyList<MarkdownHeading> Headings(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        var headings = new List<MarkdownHeading>();

        foreach (var block in Read(markdown, MarkdownOptions.Prose))
        {
            if (block.Kind != MarkdownBlockKind.Heading)
            {
                continue;
            }

            var text = Inline(block.Lines[0], html: false, MarkdownStyle.Plain, MarkdownOptions.Prose);

            headings.Add(new MarkdownHeading(block.Level, text, Slug(block.Lines[0])));
        }

        return headings;
    }

    /// <summary>The id a heading of this text wears: its words with the markup spent, accents
    /// stripped, lowercased, and every run of anything else a single hyphen.</summary>
    public static string Slug(string? heading)
    {
        if (string.IsNullOrWhiteSpace(heading))
        {
            return string.Empty;
        }

        var text = Inline(heading, html: false, MarkdownStyle.Plain, MarkdownOptions.Prose)
            .Normalize(NormalizationForm.FormD);

        var slug = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                slug.Append(char.ToLowerInvariant(character));
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        return slug.ToString().Trim('-');
    }

    /// <summary>The same source with the markup spent rather than dropped: emphasis becomes
    /// whatever the style's delimiters are, a bullet its marker, and a link its text. A null
    /// style reads as MarkdownStyle.Plain, which keeps nothing but the words.</summary>
    public static string ToText(string? markdown, MarkdownStyle? style = null)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        style ??= MarkdownStyle.Plain;

        var text = new StringBuilder();

        // Prose whichever options a page renders the same source with: an image and a table
        // that a wire cannot draw stay the characters their author typed.
        foreach (var block in Read(markdown, MarkdownOptions.Prose))
        {
            AppendText(text, block, style);
        }

        return text.ToString().TrimEnd('\n');
    }

    static void AppendHtml(StringBuilder html, MarkdownBlock block, MarkdownOptions options)
    {
        switch (block.Kind)
        {
            case MarkdownBlockKind.Heading:
                var anchor = options.Anchors
                    ? string.Create(CultureInfo.InvariantCulture, $" id=\"{Slug(block.Lines[0])}\"")
                    : string.Empty;

                html.Append(CultureInfo.InvariantCulture, $"<h{block.Level}{anchor}>{Inline(block.Lines[0], html: true, MarkdownStyle.Plain, options)}</h{block.Level}>");
                break;

            case MarkdownBlockKind.Quote:
                html.Append("<blockquote>");

                foreach (var paragraph in block.Lines)
                {
                    html.Append(CultureInfo.InvariantCulture, $"<p>{Inline(paragraph, html: true, MarkdownStyle.Plain, options)}</p>");
                }

                html.Append("</blockquote>");
                break;

            case MarkdownBlockKind.OrderedList:
            case MarkdownBlockKind.UnorderedList:
                var tag = block.Kind == MarkdownBlockKind.OrderedList ? "ol" : "ul";

                html.Append(CultureInfo.InvariantCulture, $"<{tag}>");

                foreach (var item in block.Lines)
                {
                    html.Append(CultureInfo.InvariantCulture, $"<li>{Inline(item, html: true, MarkdownStyle.Plain, options)}</li>");
                }

                html.Append(CultureInfo.InvariantCulture, $"</{tag}>");
                break;

            case MarkdownBlockKind.Table:
                AppendTable(html, block, options);
                break;

            default:
                html.Append(CultureInfo.InvariantCulture, $"<p>{Inline(block.Lines[0], html: true, MarkdownStyle.Plain, options)}</p>");
                break;
        }
    }

    static void AppendTable(StringBuilder html, MarkdownBlock block, MarkdownOptions options)
    {
        html.Append("<table><thead><tr>");

        foreach (var cell in Cells(block.Lines[0]))
        {
            html.Append(CultureInfo.InvariantCulture, $"<th>{Inline(cell, html: true, MarkdownStyle.Plain, options)}</th>");
        }

        html.Append("</tr></thead><tbody>");

        foreach (var row in block.Lines.Skip(1))
        {
            html.Append("<tr>");

            foreach (var cell in Cells(row))
            {
                html.Append(CultureInfo.InvariantCulture, $"<td>{Inline(cell, html: true, MarkdownStyle.Plain, options)}</td>");
            }

            html.Append("</tr>");
        }

        html.Append("</tbody></table>");
    }

    static void AppendText(StringBuilder text, MarkdownBlock block, MarkdownStyle style)
    {
        switch (block.Kind)
        {
            case MarkdownBlockKind.Quote:
                foreach (var paragraph in block.Lines)
                {
                    text.Append(style.Quote).Append(Inline(paragraph, html: false, style, MarkdownOptions.Prose)).Append('\n');
                }

                break;

            case MarkdownBlockKind.OrderedList:
                var number = 1;

                foreach (var item in block.Lines)
                {
                    text.Append(number.ToString(CultureInfo.InvariantCulture))
                        .Append(". ")
                        .Append(Inline(item, html: false, style, MarkdownOptions.Prose))
                        .Append('\n');
                    number++;
                }

                break;

            case MarkdownBlockKind.UnorderedList:
                foreach (var item in block.Lines)
                {
                    text.Append(style.Bullet).Append(Inline(item, html: false, style, MarkdownOptions.Prose)).Append('\n');
                }

                break;

            default:
                text.Append(Inline(block.Lines[0], html: false, style, MarkdownOptions.Prose)).Append('\n');
                break;
        }

        text.Append('\n');
    }

    static List<MarkdownBlock> Read(string markdown, MarkdownOptions options)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var blocks = new List<MarkdownBlock>();
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index];

            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            var heading = Heading.Match(line);

            if (heading.Success)
            {
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Heading, [heading.Groups[2].Value], heading.Groups[1].Value.Length));
                index++;
                continue;
            }

            if (IsQuote(line))
            {
                var quoted = new List<string>();

                while (index < lines.Length && IsQuote(lines[index]))
                {
                    quoted.Add(StripQuoteMarker(lines[index]));
                    index++;
                }

                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Quote, JoinParagraphs(quoted)));
                continue;
            }

            if (OrderedItem.IsMatch(line))
            {
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.OrderedList, ReadItems(OrderedItem, lines, ref index)));
                continue;
            }

            if (UnorderedItem.IsMatch(line))
            {
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.UnorderedList, ReadItems(UnorderedItem, lines, ref index)));
                continue;
            }

            if (IsTableHead(lines, index, options))
            {
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Table, ReadTable(lines, ref index)));
                continue;
            }

            var paragraph = new List<string>();

            while (index < lines.Length && IsParagraphLine(lines[index]) && !IsTableHead(lines, index, options))
            {
                paragraph.Add(lines[index]);
                index++;
            }

            blocks.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, [string.Join(' ', paragraph)]));
        }

        return blocks;
    }

    // The rule line is what declares a table, so the head is only a head with one under it —
    // and a caller that admits no tables never asks, which leaves both lines paragraph text.
    static bool IsTableHead(string[] lines, int index, MarkdownOptions options)
    {
        return options.Tables
            && index + 1 < lines.Length
            && TableRow.IsMatch(lines[index])
            && TableRule.IsMatch(lines[index + 1]);
    }

    static List<string> ReadTable(string[] lines, ref int index)
    {
        var rows = new List<string> { lines[index] };

        index += 2;

        while (index < lines.Length && TableRow.IsMatch(lines[index]))
        {
            rows.Add(lines[index]);
            index++;
        }

        return rows;
    }

    static IEnumerable<string> Cells(string row)
    {
        return TableRow.Match(row).Groups[1].Value.Split('|').Select(cell => cell.Trim());
    }

    static bool IsParagraphLine(string line)
    {
        return !string.IsNullOrWhiteSpace(line)
            && !Heading.IsMatch(line)
            && !IsQuote(line)
            && !OrderedItem.IsMatch(line)
            && !UnorderedItem.IsMatch(line);
    }

    static List<string> ReadItems(Regex marker, string[] lines, ref int index)
    {
        var items = new List<string>();

        while (index < lines.Length && marker.IsMatch(lines[index]))
        {
            items.Add(marker.Match(lines[index]).Groups[1].Value);
            index++;
        }

        return items;
    }

    static List<string> JoinParagraphs(List<string> lines)
    {
        var paragraphs = new List<string>();
        var current = new List<string>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                Flush(paragraphs, current);
            }
            else
            {
                current.Add(line);
            }
        }

        Flush(paragraphs, current);

        return paragraphs;
    }

    static void Flush(List<string> paragraphs, List<string> current)
    {
        if (current.Count == 0)
        {
            return;
        }

        paragraphs.Add(string.Join(' ', current));
        current.Clear();
    }

    static bool IsQuote(string line)
    {
        return line.TrimStart().StartsWith('>');
    }

    static string StripQuoteMarker(string line)
    {
        var trimmed = line.TrimStart()[1..];

        return trimmed.StartsWith(' ') ? trimmed[1..] : trimmed;
    }

    static string Inline(string text, bool html, MarkdownStyle style, MarkdownOptions options)
    {
        var rendered = html ? WebUtility.HtmlEncode(text) : text;

        // Images before links, and only where they are admitted: "![alt](src)" carries a link's
        // own shape inside it, so the link pass would otherwise eat all but the bang — which is
        // exactly what a caller that admits no images still gets, unchanged.
        if (options.Images)
        {
            rendered = Image.Replace(rendered, match => RenderImage(match, html, options));
        }

        // Links first: the anchor's own text may carry emphasis, and an emphasis pass that ran
        // ahead of it would have to be taught to keep out of an href.
        rendered = Link.Replace(rendered, match => RenderLink(match, html, style, options));
        rendered = Emphasis.Replace(rendered, match => RenderEmphasis(match, html, style));

        return rendered;
    }

    static string RenderImage(Match match, bool html, MarkdownOptions options)
    {
        var alt = match.Groups[1].Value;
        var address = match.Groups[2].Value;

        if (!IsDrawable(address))
        {
            return match.Value;
        }

        if (!html)
        {
            return alt;
        }

        return $"<img src=\"{Resolve(address, options.BasePath)}\" alt=\"{alt}\" loading=\"lazy\" />";
    }

    // A relative address is a file the document's own folder carries; anything with a scheme
    // has to be one of the two that can only fetch a picture — "javascript:" is the reason this
    // renderer never trusts its input, and a data URI is a payload rather than an address.
    static bool IsDrawable(string address)
    {
        return !address.Contains(':', StringComparison.Ordinal)
            || ImageSchemes.Any(scheme => address.StartsWith(scheme, StringComparison.OrdinalIgnoreCase));
    }

    static string Resolve(string address, string? basePath)
    {
        if (string.IsNullOrEmpty(basePath) || address.Contains(':', StringComparison.Ordinal) || address.StartsWith('/'))
        {
            return address;
        }

        return basePath.EndsWith('/') ? basePath + address : $"{basePath}/{address}";
    }

    static string RenderEmphasis(Match match, bool html, MarkdownStyle style)
    {
        if (match.Groups[1].Success)
        {
            return html
                ? $"<strong>{match.Groups[1].Value}</strong>"
                : style.Bold + match.Groups[1].Value + style.Bold;
        }

        return html
            ? $"<em>{match.Groups[2].Value}</em>"
            : style.Italic + match.Groups[2].Value + style.Italic;
    }

    static string RenderLink(Match match, bool html, MarkdownStyle style, MarkdownOptions options)
    {
        var text = match.Groups[1].Value;
        var address = match.Groups[2].Value;

        // An address whose scheme is not one of the three stays the literal text it was
        // written as: a "javascript:" href is the whole reason this renderer never trusts its
        // input, and repairing one would be guessing at what the author meant. A document
        // rendered inside the app also admits its own root-relative addresses and a bare
        // fragment — a section of the chapter already open — never a protocol-relative
        // "//host", which names somebody else's.
        var withinApp = options.AppLinks
            && (address.StartsWith('#')
                || (address.StartsWith('/') && !address.StartsWith("//", StringComparison.Ordinal)));

        if (!withinApp && !Schemes.Any(scheme => address.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)))
        {
            return match.Value;
        }

        if (html)
        {
            return $"<a href=\"{address}\" rel=\"noopener noreferrer\">{text}</a>";
        }

        return style.ShowsLinkAddress ? $"{text} ({address})" : text;
    }

    enum MarkdownBlockKind
    {
        Paragraph,
        Heading,
        Quote,
        OrderedList,
        UnorderedList,
        Table,
    }

    sealed record MarkdownBlock(MarkdownBlockKind Kind, IReadOnlyList<string> Lines, int Level = 0);
}
