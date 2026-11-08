// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using System.Xml;

namespace NSail.Icons;

/// <summary>The one allowlist an SVG passes through before it reaches the DOM: a Glyph, a
/// fetched logo (NsRemoteImage) and the splash mark all draw through it. Only drawing
/// elements and presentation attributes survive; scripts, event handlers, styles, external
/// references (<c>image</c>, <c>a</c>, <c>foreignObject</c>, non-fragment <c>href</c>) and
/// anything that is not well-formed XML are dropped, and markup with nothing left is empty.</summary>
public static class SvgSanitizer
{
    const string WrapperName = "svg";

    static readonly HashSet<string> Elements = new(StringComparer.OrdinalIgnoreCase)
    {
        "svg", "g", "path", "circle", "ellipse", "rect", "line", "polyline", "polygon",
        "defs", "linearGradient", "radialGradient", "stop", "clipPath", "mask", "use",
        "title", "desc", "text", "tspan"
    };

    static readonly HashSet<string> TextElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "title", "desc", "text", "tspan"
    };

    static readonly HashSet<string> UrlAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "fill", "stroke", "clip-path", "mask", "stop-color"
    };

    static readonly HashSet<string> Attributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "class", "d", "transform", "viewBox", "width", "height", "x", "y", "x1", "y1", "x2", "y2",
        "cx", "cy", "r", "rx", "ry", "points", "fill", "fill-opacity", "fill-rule", "clip-rule",
        "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "stroke-miterlimit",
        "stroke-dasharray", "stroke-dashoffset", "stroke-opacity", "opacity", "offset", "stop-color",
        "stop-opacity", "gradientUnits", "gradientTransform", "fx", "fy", "preserveAspectRatio",
        "clip-path", "mask", "maskUnits", "clipPathUnits", "font-family", "font-size", "font-weight",
        "text-anchor", "dx", "dy", "role", "aria-label", "aria-hidden", "focusable", "href"
    };

    /// <summary>The element names the allowlist keeps — for a client that must apply the same
    /// list where C# cannot run (the splash, drawn before the app boots).</summary>
    public static IReadOnlyCollection<string> AllowedElements => Elements.ToArray();

    /// <summary>The elements whose text content survives; see <see cref="AllowedElements"/>.</summary>
    public static IReadOnlyCollection<string> AllowedTextElements => TextElements.ToArray();

    /// <summary>Whether sanitized markup still draws something: a shape, not only the
    /// wrapper. A file that sanitizes to nothing drawable is better shown as an image.</summary>
    public static bool HasDrawing(string? clean)
    {
        if (string.IsNullOrWhiteSpace(clean) || !TryLoad($"<{WrapperName}>{clean}</{WrapperName}>", out var doc))
        {
            return false;
        }

        return DrawsIn(doc!.DocumentElement!);
    }

    // defs, clip paths, masks and gradients hold shapes that paint nothing by themselves.
    static bool DrawsIn(XmlElement parent)
    {
        foreach (XmlNode node in parent.ChildNodes)
        {
            if (node is not XmlElement e || e.LocalName is "defs" or "clipPath" or "mask" or "linearGradient" or "radialGradient")
            {
                continue;
            }

            if (e.LocalName is "path" or "circle" or "ellipse" or "rect" or "line" or "polyline" or "polygon" or "text" || DrawsIn(e))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The attribute names the allowlist keeps; see <see cref="AllowedElements"/>.</summary>
    public static IReadOnlyCollection<string> AllowedAttributes => Attributes.ToArray();

    /// <summary>The markup with everything outside the allowlist removed. A fragment (the
    /// inner content of an svg, as the icon catalogs carry) stays a fragment.</summary>
    public static string Sanitize(string? markup)
    {
        return Run(markup, out _);
    }

    /// <summary>As <see cref="Sanitize(string?)"/>, also reporting whether anything outside
    /// the allowlist had to be dropped.</summary>
    public static string Sanitize(string? markup, out bool clean)
    {
        return Run(markup, out clean);
    }

    /// <summary>Whether <paramref name="markup"/> holds only what the allowlist keeps — the
    /// test a store applies to refuse a value rather than clean it.</summary>
    public static bool IsClean(string? markup)
    {
        Run(markup, out var clean);

        return clean;
    }

    static string Run(string? markup, out bool clean)
    {
        clean = true;

        if (string.IsNullOrWhiteSpace(markup))
        {
            return string.Empty;
        }

        var wrapped = false;

        if (!TryLoad(markup, out var doc))
        {
            wrapped = true;

            if (!TryLoad($"<{WrapperName}>{markup}</{WrapperName}>", out doc))
            {
                clean = false;

                return string.Empty;
            }
        }

        var output = new StringBuilder();
        var settings = new XmlWriterSettings { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment };
        var ok = true;

        using (var writer = XmlWriter.Create(output, settings))
        {
            if (wrapped)
            {
                foreach (XmlNode child in doc!.DocumentElement!.ChildNodes)
                {
                    Write(writer, child, ref ok);
                }
            }
            else
            {
                Write(writer, doc!.DocumentElement!, ref ok);
            }
        }

        clean = ok;

        return output.ToString();
    }

    static bool TryLoad(string xml, out XmlDocument? doc)
    {
        doc = new XmlDocument { XmlResolver = null };

        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };

            using var reader = XmlReader.Create(new StringReader(xml), settings);
            doc.Load(reader);

            return doc.DocumentElement is not null;
        }
        catch (XmlException)
        {
            doc = null;

            return false;
        }
    }

    static void Write(XmlWriter writer, XmlNode node, ref bool clean)
    {
        if (node is XmlText or XmlSignificantWhitespace or XmlWhitespace or XmlCDataSection)
        {
            if (node.ParentNode is { } parent && TextElements.Contains(parent.LocalName))
            {
                writer.WriteString(node.Value);
            }
            else if (node is not (XmlWhitespace or XmlSignificantWhitespace))
            {
                clean = false;
            }

            return;
        }

        if (node is not XmlElement element)
        {
            if (node is not XmlComment)
            {
                clean = false;
            }

            return;
        }

        if (!Elements.Contains(element.LocalName) || !IsSvgNamespace(element.NamespaceURI))
        {
            clean = false;

            return;
        }

        writer.WriteStartElement(element.LocalName);

        foreach (XmlAttribute attribute in element.Attributes)
        {
            if (attribute.Prefix == "xmlns" || attribute.Name == "xmlns")
            {
                continue;
            }

            if (IsAllowed(attribute))
            {
                writer.WriteAttributeString(attribute.Name, attribute.Value);
            }
            else
            {
                clean = false;
            }
        }

        foreach (XmlNode child in element.ChildNodes)
        {
            Write(writer, child, ref clean);
        }

        writer.WriteFullEndElement();
    }

    static bool IsData(string name)
    {
        return name.StartsWith("data-", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsSvgNamespace(string ns)
    {
        return ns.Length == 0 || ns == "http://www.w3.org/2000/svg";
    }

    static bool IsAllowed(XmlAttribute attribute)
    {
        if (!Attributes.Contains(attribute.Name) && !IsData(attribute.Name))
        {
            return false;
        }

        var value = attribute.Value;

        if (attribute.LocalName == "href")
        {
            return value.StartsWith('#');
        }

        // url(...) in paint and clip properties is the one place a value can reach out.
        return !value.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
            && !(UrlAttributes.Contains(attribute.LocalName) && value.Contains("data:", StringComparison.OrdinalIgnoreCase))
            && (!value.Contains("url(", StringComparison.OrdinalIgnoreCase) || IsLocalUrl(value));
    }

    static bool IsLocalUrl(string value)
    {
        var rest = value;

        for (var at = rest.IndexOf("url(", StringComparison.OrdinalIgnoreCase); at >= 0; at = rest.IndexOf("url(", StringComparison.OrdinalIgnoreCase))
        {
            rest = rest[(at + 4)..].TrimStart(' ', '\'', '"');

            if (!rest.StartsWith('#'))
            {
                return false;
            }
        }

        return true;
    }
}
