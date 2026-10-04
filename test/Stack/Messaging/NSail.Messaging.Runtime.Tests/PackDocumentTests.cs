// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Packs;
using Xunit;

namespace NSail.Messaging.Runtime.Tests;

// The document law, held where it lives (nsail#1119): every guid in a pack's text is answered
// for the importing scope before the text is a Pack — the one it names, the one it creates,
// the one it points back at — and a text that is not a pack says so by name.
public sealed class PackDocumentTests
{
    [Fact]
    public void Every_guid_in_the_document_is_answered_for_the_importing_scope()
    {
        var named = Guid.Parse("442c0000-0000-0000-0000-000000000018");
        var pointed = Guid.Parse("442b0000-0000-0000-0000-000000000001");
        var body = "{\"steps\":[{\"message\":\"x\",\"body\":{\"Id\":\"" + pointed + "\",\"CountryId\":\"" + named + "\"}}]}";
        var seen = new List<Guid>();

        var resolved = PackDocument.Resolve(body, id =>
        {
            seen.Add(id);

            return Guid.Empty;
        });

        Assert.Equal([pointed, named], seen);
        Assert.DoesNotContain(named.ToString(), resolved, StringComparison.Ordinal);
        Assert.Equal(2, resolved.Split(Guid.Empty.ToString()).Length - 1);
    }

    [Fact]
    public void A_resolved_document_parses_to_its_steps()
    {
        var pack = PackDocument.Parse("""{"name":"p","steps":[{"message":"Directory.Geography.CreateCountry","body":{"Code":"AR"}}]}""", "p");

        Assert.Equal("p", pack.Name);
        Assert.Single(pack.Steps);
        Assert.Equal("Directory.Geography.CreateCountry", pack.Steps[0].Message);
    }

    [Fact]
    public void A_text_that_is_no_pack_names_itself_in_the_refusal()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => PackDocument.Parse("null", "the-file"));

        Assert.Contains("the-file", exception.Message, StringComparison.Ordinal);
    }
}
