// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components;
using NSail.Text;

namespace NSail.Components.Tests;

public sealed class MarkdownTests
{
    [Theory]
    [InlineData("# Title", "<h1>Title</h1>")]
    [InlineData("###### Title", "<h6>Title</h6>")]
    [InlineData("## 1. Partes", "<h2>1. Partes</h2>")]
    public void ToHtml_renders_headings_by_level(string markdown, string expected)
    {
        Assert.Equal(expected, Markdown.ToHtml(markdown).Value);
    }

    // The anchor is a document's, and this entry point's default is not one: a message and a
    // wizard step render the same heading with nothing to link to.
    [Theory]
    [InlineData("# Title", "<h1 id=\"title\">Title</h1>")]
    [InlineData("## 1. Partes", "<h2 id=\"1-partes\">1. Partes</h2>")]
    public void ToHtml_gives_a_document_heading_the_id_of_its_own_slug(string markdown, string expected)
    {
        Assert.Equal(expected, Markdown.ToHtml(markdown, MarkdownOptions.Document).Value);
    }

    [Fact]
    public void ToHtml_renders_a_paragraph()
    {
        var html = Markdown.ToHtml("Plain text.").Value;

        Assert.Equal("<p>Plain text.</p>", html);
    }

    [Fact]
    public void ToHtml_joins_soft_wrapped_lines_into_one_paragraph()
    {
        var html = Markdown.ToHtml("Line one\nline two.").Value;

        Assert.Equal("<p>Line one line two.</p>", html);
    }

    [Fact]
    public void ToHtml_separates_paragraphs_on_a_blank_line()
    {
        var html = Markdown.ToHtml("First.\n\nSecond.").Value;

        Assert.Equal("<p>First.</p><p>Second.</p>", html);
    }

    [Fact]
    public void ToHtml_renders_a_blockquote_as_one_paragraph()
    {
        var html = Markdown.ToHtml("> Line one\n> line two.").Value;

        Assert.Equal("<blockquote><p>Line one line two.</p></blockquote>", html);
    }

    [Fact]
    public void ToHtml_renders_an_unordered_list()
    {
        var html = Markdown.ToHtml("- One\n- Two\n* Three").Value;

        Assert.Equal("<ul><li>One</li><li>Two</li><li>Three</li></ul>", html);
    }

    [Fact]
    public void ToHtml_renders_an_ordered_list()
    {
        var html = Markdown.ToHtml("1. One\n2. Two\n10. Ten").Value;

        Assert.Equal("<ol><li>One</li><li>Two</li><li>Ten</li></ol>", html);
    }

    [Fact]
    public void ToHtml_renders_bold_and_italic_inline()
    {
        var html = Markdown.ToHtml("**bold** and *italic*.").Value;

        Assert.Equal("<p><strong>bold</strong> and <em>italic</em>.</p>", html);
    }

    [Fact]
    public void ToHtml_html_encodes_source_text_before_wrapping_tags()
    {
        var html = Markdown.ToHtml("<script>alert('x')</script> & co").Value;

        Assert.Equal("<p>&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt; &amp; co</p>", html);
    }

    [Fact]
    public void ToHtml_returns_empty_for_null_or_blank_input()
    {
        Assert.Equal(string.Empty, Markdown.ToHtml(null).Value);
        Assert.Equal(string.Empty, Markdown.ToHtml("   ").Value);
    }

    [Fact]
    public void ToHtml_renders_the_contract_shape_end_to_end()
    {
        var markdown =
            "# Contrato de servicio\n\n" +
            "> **Borrador operativo** -- aviso legal\n" +
            "> segunda linea del aviso.\n\n" +
            "## 1. Partes\n\n" +
            "Texto con **negrita** y *cursiva*.\n";

        var html = Markdown.ToHtml(markdown).Value;

        Assert.Equal(
            "<h1>Contrato de servicio</h1>" +
            "<blockquote><p><strong>Borrador operativo</strong> -- aviso legal segunda linea del aviso.</p></blockquote>" +
            "<h2>1. Partes</h2>" +
            "<p>Texto con <strong>negrita</strong> y <em>cursiva</em>.</p>",
            html);
    }
}
