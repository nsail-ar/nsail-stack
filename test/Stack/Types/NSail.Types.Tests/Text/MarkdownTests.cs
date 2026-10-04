// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Text;

namespace NSail.Types.Tests.Text;

public sealed class MarkdownTests
{
    [Fact]
    public void ToHtml_renders_a_link_when_the_scheme_is_one_of_the_three()
    {
        var html = Markdown.ToHtml("Read the [notice](https://example.com/n).");

        Assert.Equal("<p>Read the <a href=\"https://example.com/n\" rel=\"noopener noreferrer\">notice</a>.</p>", html);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](/relative)")]
    public void ToHtml_leaves_a_link_of_another_scheme_as_literal_text(string markdown)
    {
        var html = Markdown.ToHtml(markdown);

        Assert.DoesNotContain("<a ", html, StringComparison.Ordinal);
    }

    // nsail#930: a chapter of the guide names another chapter by its own address, so the
    // document a page draws admits an address rooted at the app — which is the one case where
    // "/" is known to mean this host. Text on the send path still has no host to resolve it
    // against, which is the theory above.
    [Fact]
    public void ToHtml_renders_an_app_rooted_link_in_a_document()
    {
        var html = Markdown.ToHtml("It is in [Accounting](/guide/accounting).", MarkdownOptions.Document);

        Assert.Equal(
            "<p>It is in <a href=\"/guide/accounting\" rel=\"noopener noreferrer\">Accounting</a>.</p>",
            html);
    }

    [Theory]
    [InlineData("[x](//evil.example/steal)")]
    [InlineData("[x](javascript:alert(1))")]
    public void ToHtml_leaves_an_address_that_is_not_this_app_literal_even_in_a_document(string markdown)
    {
        var html = Markdown.ToHtml(markdown, MarkdownOptions.Document);

        Assert.DoesNotContain("<a ", html, StringComparison.Ordinal);
    }

    // nsail#1370: a chapter links to one of its own sections the same way a heading's own id is
    // built — a bare fragment, no chapter in front of it — and the renderer refused every one of
    // them, admitting only an address rooted at "/". The three that ship today read as their own
    // markdown source until this case is admitted.
    [Fact]
    public void ToHtml_renders_a_same_chapter_fragment_link_in_a_document()
    {
        var html = Markdown.ToHtml("See the [QR](#el-qr-del-comprobante).", MarkdownOptions.Document);

        Assert.Equal(
            "<p>See the <a href=\"#el-qr-del-comprobante\" rel=\"noopener noreferrer\">QR</a>.</p>",
            html);
    }

    // A fragment names a place inside the document being drawn; a WhatsApp body has no document
    // to land it in, which is a sharper reason than the rooted case's missing host.
    [Fact]
    public void ToHtml_leaves_a_fragment_link_literal_where_the_caller_admits_none()
    {
        var html = Markdown.ToHtml("See the [QR](#el-qr-del-comprobante).");

        Assert.Equal("<p>See the [QR](#el-qr-del-comprobante).</p>", html);
    }

    [Fact]
    public void ToText_keeps_the_words_and_spends_the_markup()
    {
        var text = Markdown.ToText("# Title\n\nSome **bold** and *thin* words.");

        Assert.Equal("Title\n\nSome bold and thin words.", text);
    }

    [Fact]
    public void ToText_writes_a_link_as_its_text_and_its_address()
    {
        var text = Markdown.ToText("Read the [notice](https://example.com/n).");

        Assert.Equal("Read the notice (https://example.com/n).", text);
    }

    [Fact]
    public void ToText_drops_a_link_address_when_the_style_says_so()
    {
        var text = Markdown.ToText(
            "Read the [notice](https://example.com/n).",
            new MarkdownStyle { ShowsLinkAddress = false });

        Assert.Equal("Read the notice.", text);
    }

    [Fact]
    public void ToText_marks_a_list_with_the_style_own_bullet()
    {
        var text = Markdown.ToText("- One\n- Two", new MarkdownStyle { Bullet = "• " });

        Assert.Equal("• One\n• Two", text);
    }

    [Fact]
    public void ToText_numbers_an_ordered_list_from_one()
    {
        var text = Markdown.ToText("1. One\n7. Two");

        Assert.Equal("1. One\n2. Two", text);
    }

    [Fact]
    public void ToText_wraps_emphasis_in_the_style_own_delimiters()
    {
        var text = Markdown.ToText(
            "**loud** and *thin*",
            new MarkdownStyle { Bold = "*", Italic = "_" });

        Assert.Equal("*loud* and _thin_", text);
    }

    [Fact]
    public void ToText_does_not_encode_html_because_no_markup_survives_it()
    {
        var text = Markdown.ToText("a & b");

        Assert.Equal("a & b", text);
    }

    [Fact]
    public void Both_projections_answer_empty_for_nothing()
    {
        Assert.Equal(string.Empty, Markdown.ToHtml(null));
        Assert.Equal(string.Empty, Markdown.ToText("   "));
    }

    [Fact]
    public void ToHtml_draws_an_image_where_a_document_admits_one()
    {
        var html = Markdown.ToHtml("![La caja](caja.png)", MarkdownOptions.Document);

        Assert.Equal("<p><img src=\"caja.png\" alt=\"La caja\" loading=\"lazy\" /></p>", html);
    }

    [Fact]
    public void ToHtml_resolves_a_relative_image_against_the_document_own_folder()
    {
        var html = Markdown.ToHtml(
            "![La caja](caja.png)",
            MarkdownOptions.Document with { BasePath = "_content/NSail.Sales.Shared/guide/es" });

        Assert.Contains("src=\"_content/NSail.Sales.Shared/guide/es/caja.png\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("![x](javascript:alert(1))")]
    [InlineData("![x](data:image/png;base64,AAAA)")]
    public void ToHtml_leaves_an_image_of_another_scheme_as_literal_text(string markdown)
    {
        var html = Markdown.ToHtml(markdown, MarkdownOptions.Document);

        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
    }

    // The send path renders the same source and reads exactly what it always read: no image,
    // and an addressable link still an anchor after the bang, which is what makes the option
    // safe to add under a path nobody re-tested.
    [Fact]
    public void ToHtml_leaves_an_image_alone_where_the_caller_admits_none()
    {
        Assert.Equal(
            "<p>!<a href=\"https://example.com/caja.png\" rel=\"noopener noreferrer\">La caja</a></p>",
            Markdown.ToHtml("![La caja](https://example.com/caja.png)"));

        Assert.Equal("<p>![La caja](caja.png)</p>", Markdown.ToHtml("![La caja](caja.png)"));
    }

    [Fact]
    public void ToText_writes_an_image_as_the_characters_it_was_typed_as()
    {
        var text = Markdown.ToText("Mirá ![La caja](caja.png) acá.");

        Assert.Equal("Mirá ![La caja](caja.png) acá.", text);
    }

    [Fact]
    public void ToHtml_draws_a_pipe_table_where_a_document_admits_one()
    {
        var html = Markdown.ToHtml("| Tipo | Uso |\n|---|---|\n| A | Todo |\n| B | Nada |", MarkdownOptions.Document);

        Assert.Equal(
            "<table><thead><tr><th>Tipo</th><th>Uso</th></tr></thead>"
                + "<tbody><tr><td>A</td><td>Todo</td></tr><tr><td>B</td><td>Nada</td></tr></tbody></table>",
            html);
    }

    [Fact]
    public void ToHtml_leaves_a_pipe_table_alone_where_the_caller_admits_none()
    {
        var html = Markdown.ToHtml("| Tipo | Uso |\n|---|---|\n| A | Todo |");

        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
        Assert.Contains("| Tipo | Uso |", html, StringComparison.Ordinal);
    }

    // A pipe is an ordinary character until a rule line under it says otherwise.
    [Fact]
    public void ToHtml_reads_a_row_with_no_rule_under_it_as_a_paragraph()
    {
        var html = Markdown.ToHtml("| Tipo | Uso |\n| A | Todo |", MarkdownOptions.Document);

        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToHtml_encodes_a_table_cell_like_any_other_text()
    {
        var html = Markdown.ToHtml("| a |\n|---|\n| <script>x</script> |", MarkdownOptions.Document);

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToHtml_gives_every_heading_the_id_of_its_own_slug()
    {
        var html = Markdown.ToHtml("## Cobrar una Orden de trabajo", MarkdownOptions.Document);

        Assert.Equal("<h2 id=\"cobrar-una-orden-de-trabajo\">Cobrar una Orden de trabajo</h2>", html);
    }

    // An anchor belongs to a document, and the same renderer draws things that are not one — a
    // message, a wizard step's line. What they render is exactly what it was before a guide
    // needed somewhere to land.
    [Fact]
    public void ToHtml_leaves_a_heading_bare_where_nothing_can_be_linked_to()
    {
        Assert.Equal(
            "<h2>Cobrar una Orden de trabajo</h2>",
            Markdown.ToHtml("## Cobrar una Orden de trabajo"));
    }

    [Fact]
    public void Slug_strips_accents_markup_and_punctuation()
    {
        Assert.Equal("la-cotizacion-del-dolar", Markdown.Slug("La cotización del dólar"));
        Assert.Equal("registrar-una-venta-con-descuento", Markdown.Slug("Registrar una **Venta** (con descuento)"));
    }

    [Fact]
    public void Headings_reads_the_index_a_document_carries()
    {
        var headings = Markdown.Headings("## Cobrar\n\nTexto.\n\n### La cotización del dólar\n\n## Libros");

        Assert.Equal([2, 3, 2], headings.Select(heading => heading.Level));
        Assert.Equal(["Cobrar", "La cotización del dólar", "Libros"], headings.Select(heading => heading.Text));
        Assert.Equal(["cobrar", "la-cotizacion-del-dolar", "libros"], headings.Select(heading => heading.Slug));
    }

    // The index and the anchors it points at are one derivation: every slug the reader hands
    // out has an id in the rendered document under exactly that name.
    [Fact]
    public void Every_heading_slug_names_an_id_the_html_carries()
    {
        const string markdown = "## Registrar una **Venta** (con descuento)\n\nTexto.\n\n### La cotización del dólar";

        var html = Markdown.ToHtml(markdown, MarkdownOptions.Document);

        foreach (var heading in Markdown.Headings(markdown))
        {
            Assert.Contains($"id=\"{heading.Slug}\"", html, StringComparison.Ordinal);
        }
    }
}
