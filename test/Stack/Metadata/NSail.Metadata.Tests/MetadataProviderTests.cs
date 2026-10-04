// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Metadata;

namespace NSail.Metadata.Tests;

public sealed class MetadataProviderTests
{
    private sealed class MailingPolicy : MetadataProvider
    {
        public override TypeMetadata Get(Type type)
        {
            return type.Namespace?.StartsWith("System.Net.Mail") == true
                ? base.Get(type) with { Area = "Mailing" }
                : base.Get(type);
        }
    }

    private sealed class OwnImplementation : MetadataProvider
    {
        public static void ParseTemplate(string template)
        {
            Parse(template);
        }
    }

    [Fact]
    public void FromName_binds_tokens_and_star_swallows_technical_segments()
    {
        var metadata = new MetadataProvider().FromName("NSail.Directory.Parties.Models.PartyModel");

        Assert.Equal("NSail", metadata.Root);
        Assert.Equal("Directory", metadata.Area);
        Assert.Equal("Parties", metadata.Feature);
        Assert.Null(metadata.SubFeature);
        Assert.Equal("PartyModel", metadata.Object);
    }

    [Fact]
    public void FromName_leaves_unmatched_tokens_null_on_short_namespaces()
    {
        var metadata = new MetadataProvider().FromName("NSail.Components.NsButton");

        Assert.Equal("NSail", metadata.Root);
        Assert.Equal("Components", metadata.Area);
        Assert.Null(metadata.Feature);
        Assert.Equal("NsButton", metadata.Object);
    }

    [Fact]
    public void FromName_works_for_any_company_root()
    {
        var metadata = new MetadataProvider().FromName("Batman.Contabilidad.Sarlanga.Asiento");

        Assert.Equal("Batman", metadata.Root);
        Assert.Equal("Contabilidad", metadata.Area);
        Assert.Equal("Sarlanga", metadata.Feature);
        Assert.Equal("Asiento", metadata.Object);
    }

    [Fact]
    public void FromName_with_a_single_segment_binds_only_the_object()
    {
        var metadata = new MetadataProvider().FromName("GlobalModel");

        Assert.Null(metadata.Root);
        Assert.Null(metadata.Area);
        Assert.Equal("GlobalModel", metadata.Object);
    }

    [Fact]
    public void Get_uses_the_type_full_name_and_strips_generic_arity()
    {
        var metadata = new MetadataProvider().Get(typeof(List<string>));

        Assert.Equal("System", metadata.Root);
        Assert.Equal("Collections", metadata.Area);
        Assert.Equal("Generic", metadata.Feature);
        Assert.Equal("List", metadata.Object);
    }

    [Theory]
    [InlineData("{Root}.*.{Feature}.*.{Object}")]
    [InlineData("{Root}.{Banana}.{Object}")]
    [InlineData("{Root}.{Root}.{Object}")]
    [InlineData("{Root}.Module.{Object}")]
    public void Invalid_templates_are_rejected(string template)
    {
        Assert.Throws<ArgumentException>(() => OwnImplementation.ParseTemplate(template));
    }

    [Fact]
    public void KeyFor_composes_area_and_object()
    {
        var provider = new MetadataProvider();

        Assert.Equal("Metadata.MetadataProviderTests", provider.KeyFor(typeof(MetadataProviderTests)));
        Assert.Equal("Metadata.MetadataProviderTests.Title", provider.KeyFor(typeof(MetadataProviderTests), "Title"));
    }

    [Fact]
    public void KeyFor_strips_generic_arity_through_the_metadata()
    {
        var provider = new MetadataProvider();

        Assert.Equal("Collections.List", provider.KeyFor(typeof(List<string>)));
    }

    [Fact]
    public void An_assembly_attribute_declares_the_template_for_its_types()
    {
        var metadata = new MetadataProvider().Get(typeof(MetadataProviderTests));

        Assert.Equal("Tests", metadata.SubFeature);
        Assert.Null(metadata.Feature);
    }

    [Fact]
    public void FromName_applies_the_default_template_not_the_assembly_attribute()
    {
        var metadata = new MetadataProvider().FromName("NSail.Metadata.Tests.MetadataProviderTests");

        Assert.Equal("Tests", metadata.Feature);
        Assert.Null(metadata.SubFeature);
    }

    [Fact]
    public void A_custom_policy_redirects_third_party_types()
    {
        var provider = new MailingPolicy();

        Assert.Equal("Mailing", provider.Get(typeof(System.Net.Mail.MailMessage)).Area);
        Assert.Equal("Mailing.MailMessage", provider.KeyFor(typeof(System.Net.Mail.MailMessage)));

        Assert.Equal("Metadata.TypeMetadata", provider.KeyFor(typeof(TypeMetadata)));
    }
}
