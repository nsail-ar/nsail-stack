// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data.Tests;

public class TenantSlugTests
{
    [Theory]
    [InlineData("lumina", "lumina")]
    [InlineData("l", "l")]
    [InlineData("optica-lumina", "optica-lumina")]
    [InlineData("demo2026", "demo2026")]
    // A host label is case-insensitive, so folding case is normalization, not repair.
    [InlineData("Lumina", "lumina")]
    [InlineData("LUMINA", "lumina")]
    public void TheWorkingSetPassesThrough(string header, string slug)
    {
        Assert.Equal(slug, TenantSlug.Normalize(header));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("lumina lumina")]
    [InlineData("lumina_lumina")]
    [InlineData("lumina.lumina")]
    [InlineData("lumina/../postgres")]
    [InlineData("lumina;DROP DATABASE optical_sa1_vision")]
    [InlineData("lumina\"")]
    [InlineData("-lumina")]
    [InlineData("lumina-")]
    [InlineData("óptica")]
    [InlineData("optical_sa1_lumina")]
    // U+212A KELVIN SIGN lowercases to 'k' under ToLowerInvariant — folding case ahead of the
    // charset check would let it repair into the working set instead of being refused.
    [InlineData("Kirk")]
    public void AnythingElseIsRefusedRatherThanRepaired(string? header)
    {
        Assert.Null(TenantSlug.Normalize(header));
    }

    // Refusing rather than stripping is the whole point: two headers that differ must never
    // arrive at one tenant, and every repair a sanitizer could make folds them together.
    [Theory]
    [InlineData("lumina!", "lumina")]
    [InlineData("lu mina", "lumina")]
    [InlineData("lumina%00", "lumina")]
    public void NoRefusedHeaderCollapsesOntoAValidOne(string header, string neighbour)
    {
        Assert.Null(TenantSlug.Normalize(header));
        Assert.Equal(neighbour, TenantSlug.Normalize(neighbour));
    }

    [Fact]
    public void NothingLongerThanAnIdentifierIsASlug()
    {
        Assert.NotNull(TenantSlug.Normalize(new string('a', TenantSlug.MaxLength)));
        Assert.Null(TenantSlug.Normalize(new string('a', TenantSlug.MaxLength + 1)));
    }
}
