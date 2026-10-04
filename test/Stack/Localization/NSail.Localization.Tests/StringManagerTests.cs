// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Localization;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Localization.Tests;

public sealed class StringManagerTests
{
    private sealed class FakeSource(string language, IReadOnlyDictionary<string, string> entries) : IStringSource
    {
        static readonly IReadOnlyDictionary<string, string> _empty = new Dictionary<string, string>();

        public IReadOnlyCollection<string> Languages => [language];

        public Task<IReadOnlyDictionary<string, string>> GetStrings(string requested)
        {
            return Task.FromResult(requested == language ? entries : _empty);
        }
    }

    private static StringManager Build(string culture, params IStringSource[] sources)
    {
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        return new StringManager(new StringCatalog(sources), new LanguageProvider(), new MetadataProvider());
    }

    private static Issue Missing(string entityKey)
    {
        return new Issue(
            "NotFound",
            "server text",
            "Store",
            new Dictionary<string, string> { ["entity"] = entityKey, ["id"] = "7" });
    }

    [Fact]
    public void Translate_resolves_the_current_language()
    {
        var manager = Build("es", new FakeSource("es", new Dictionary<string, string> { ["Greeting"] = "Hola" }));

        Assert.Equal("Hola", manager.Translate("Greeting"));
    }

    [Fact]
    public void Translate_returns_key_when_missing()
    {
        var manager = Build("en", new FakeSource("en", new Dictionary<string, string>()));

        Assert.Equal("Common.Missing", manager.Translate("Common.Missing"));
    }

    [Fact]
    public void Translate_prefers_explicit_fallback_over_key()
    {
        var manager = Build("en", new FakeSource("en", new Dictionary<string, string>()));

        Assert.Equal("literal", manager.Translate("Common.Missing", "literal"));
    }

    [Fact]
    public void Later_sources_override_earlier_ones()
    {
        var kit = new FakeSource("en", new Dictionary<string, string> { ["Title"] = "Kit" });
        var product = new FakeSource("en", new Dictionary<string, string> { ["Title"] = "Product" });

        var manager = Build("en", kit, product);

        Assert.Equal("Product", manager.Translate("Title"));
    }

    [Fact]
    public void Translate_issue_prefers_the_source_scoped_key()
    {
        var manager = Build("en", new FakeSource("en", new Dictionary<string, string>
        {
            ["Problems.RuleViolation"] = "generic",
            ["Problems.RuleViolation.HasChildren"] = "delete the children first"
        }));

        var issue = new Issue("RuleViolation", "english from the server", "HasChildren");

        Assert.Equal("delete the children first", manager.Translate(issue));
    }

    [Fact]
    public void Translate_issue_falls_back_to_the_code_then_the_message()
    {
        var manager = Build("en", new FakeSource("en", new Dictionary<string, string>
        {
            ["Problems.Required"] = "Required"
        }));

        Assert.Equal("Required", manager.Translate(new Issue("Required", "server text", "Name")));
        Assert.Equal("server text", manager.Translate(new Issue("Unmapped", "server text", "Name")));
    }

    [Fact]
    public void Translate_issue_fills_named_tokens()
    {
        var manager = Build("en", new FakeSource("en", new Dictionary<string, string>
        {
            ["Problems.OutOfRange"] = "must be between {from} and {to}"
        }));

        var issue = new Issue(
            "OutOfRange",
            "server text",
            "Age",
            new Dictionary<string, string> { ["from"] = "1", ["to"] = "10" });

        Assert.Equal("must be between 1 and 10", manager.Translate(issue));
    }

    [Fact]
    public void Translate_issue_leaves_unmatched_tokens_alone()
    {
        var manager = Build("en", new FakeSource("en", new Dictionary<string, string>
        {
            ["Problems.MaxLength"] = "no more than {max}"
        }));

        Assert.Equal("no more than {max}", manager.Translate(new Issue("MaxLength", "server text")));
    }

    // What nsail#856 was about: {entity} used to be filled with the CLR type name, so a Spanish
    // counter read "No se encontró Store". The argument is the concept's key and the label is
    // resolved here, because the browser has no entity assembly to derive one from.
    [Fact]
    public void Translate_issue_reads_the_entity_argument_as_a_concept_key()
    {
        var manager = Build("es", new FakeSource("es", new Dictionary<string, string>
        {
            ["Problems.NotFound"] = "No se encontró {entity}",
            ["Products.Store"] = "Depósito",
            ["Accounting.VoucherType"] = "Tipo de Comprobante"
        }));

        Assert.Equal("No se encontró Depósito", manager.Translate(Missing("Products.Store")));
        Assert.Equal("No se encontró Tipo de Comprobante", manager.Translate(Missing("Accounting.VoucherType")));
    }

    [Fact]
    public void Translate_issue_falls_back_to_the_type_name_when_the_concept_has_no_label()
    {
        var manager = Build("es", new FakeSource("es", new Dictionary<string, string>
        {
            ["Problems.NotFound"] = "No se encontró {entity}"
        }));

        Assert.Equal("No se encontró Store", manager.Translate(Missing("Products.Store")));
    }

    [Fact]
    public void Translate_count_picks_the_plural_variant()
    {
        var manager = Build("en", new FakeSource("en", new Dictionary<string, string>
        {
            ["Children"] = "{0} child",
            ["Children.Plural"] = "{0} children"
        }));

        Assert.Equal("{0} child", manager.Translate("Children", 1));
        Assert.Equal("{0} children", manager.Translate("Children", 2));
        Assert.Equal("{0} children", manager.Translate("Children", 0));
    }

    [Fact]
    public void Translate_count_falls_back_when_there_is_no_plural()
    {
        var manager = Build("en", new FakeSource("en", new Dictionary<string, string>
        {
            ["Children"] = "{0} children"
        }));

        Assert.Equal("{0} children", manager.Translate("Children", 5));
    }

    [Fact]
    public void Catalog_serves_each_language_independently()
    {
        var catalog = new StringCatalog(new[]
        {
            new FakeSource("es", new Dictionary<string, string> { ["Greeting"] = "Hola" }),
            new FakeSource("en", new Dictionary<string, string> { ["Greeting"] = "Hello" })
        });

        var language = new LanguageProvider { Current = "es" };
        var manager = new StringManager(catalog, language, new MetadataProvider());

        Assert.Equal("Hola", manager.Translate("Greeting"));

        language.Current = "en";

        Assert.Equal("Hello", manager.Translate("Greeting"));
    }

    [Fact]
    public void TryTranslate_reports_presence()
    {
        var manager = Build("en", new FakeSource("en", new Dictionary<string, string> { ["Greeting"] = "Hello" }));

        Assert.True(manager.TryTranslate("Greeting", out var found));
        Assert.Equal("Hello", found);

        Assert.False(manager.TryTranslate("Missing", out var missing));
        Assert.Equal(string.Empty, missing);
    }
}
