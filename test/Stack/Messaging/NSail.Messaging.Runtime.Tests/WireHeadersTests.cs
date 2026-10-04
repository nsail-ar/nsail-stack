// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Context;

namespace NSail.Messaging.Runtime.Tests;

// The wire shape itself, at the altitude where both ends of a hop have to agree: what one side
// writes the other reads back under the name the caller passed, and nothing else on the request
// is mistaken for a delivery's.
public sealed class WireHeadersTests
{
    [Fact]
    public void AHeaderCrossesUnderThePrefixAndComesBackWithItsOwnName()
    {
        var wire = WireHeaders.Name(MessageHeaders.Source);

        Assert.Equal($"{WireHeaders.Prefix}{MessageHeaders.Source}", wire);
        Assert.Equal(MessageHeaders.Source, WireHeaders.Logical(wire!));
    }

    // HTTP names are matched case insensitively and so is MessageContext, so a proxy or a client
    // that re-cases the prefix still hands the far side the header the caller sent.
    [Theory]
    [InlineData("X-NSail-Source", "Source")]
    [InlineData("x-nsail-source", "source")]
    [InlineData("X-NSAIL-Trace", "Trace")]
    public void ADeliverysHeaderIsRecognizedHoweverTheWireCasedIt(string wire, string logical)
    {
        Assert.Equal(logical, WireHeaders.Logical(wire));
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("Content-Type")]
    [InlineData("X-Forwarded-For")]
    [InlineData("X-NSail-")]
    public void AHeaderThatIsNotADeliverysIsNotOne(string name)
    {
        Assert.Null(WireHeaders.Logical(name));
    }

    // Reserved on both ends rather than filtered on one: the proxy writes the tenant header and
    // the install reads it there, so a delivery neither stamps it nor arrives carrying one.
    [Fact]
    public void TheTenantHeaderCrossesInNeitherDirection()
    {
        Assert.Null(WireHeaders.Name("Tenant"));
        Assert.Null(WireHeaders.Name("tenant"));
        Assert.Null(WireHeaders.Logical(WireHeaders.Tenant));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnUnnamedHeaderTravelsNowhere(string header)
    {
        Assert.Null(WireHeaders.Name(header));
    }
}
