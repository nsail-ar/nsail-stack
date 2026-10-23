// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Text;

namespace NSail.Types.Tests.Text;

// The one rule every surface that draws an address nobody derived asks: whether it may become a
// door at all. Its two callers are the markdown renderer, over a document's own links, and the
// drawer, over a Url a module contributed.
public sealed class WebAddressTests
{
    [Theory]
    [InlineData("https://telemetry.nsail.ar")]
    [InlineData("http://telemetry.nsail.ar/path?q=1#top")]
    [InlineData("HTTPS://TELEMETRY.NSAIL.AR")]
    [InlineData("mailto:hola@nsail.ar")]
    public void A_scheme_that_opens_a_page_or_a_letter_is_navigable(string address)
    {
        Assert.True(WebAddress.IsNavigable(address, withinApp: false));
        Assert.True(WebAddress.IsNavigable(address, withinApp: true));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://host/f")]
    public void Every_other_scheme_is_not(string address)
    {
        Assert.False(WebAddress.IsNavigable(address, withinApp: false));
        Assert.False(WebAddress.IsNavigable(address, withinApp: true));
    }

    // A browser strips whitespace and control characters before it reads the scheme, so what it
    // would open is not what the value spells: neither is admitted, and neither is cleaned off.
    [Theory]
    [InlineData(" javascript:alert(1)")]
    [InlineData("java\tscript:alert(1)")]
    [InlineData("java\nscript:alert(1)")]
    [InlineData("javascript\u0000:alert(1)")]
    [InlineData("\u0001https://nsail.ar")]
    [InlineData(" /sales/boxes")]
    public void Whitespace_and_control_characters_are_refused_rather_than_trimmed(string address)
    {
        Assert.False(WebAddress.IsNavigable(address, withinApp: true));
    }

    // The slant a browser's URL parser reads as the other one while it resolves the authority:
    // "/\evil.example" resolved against this app's address is "https://evil.example", so the one
    // leading slash buys nothing. SafeReturnTo refuses the same shape of a return address.
    [Theory]
    [InlineData("/\\evil.example")]
    [InlineData("/\\\\evil.example")]
    [InlineData("\\\\evil.example")]
    [InlineData("/\\/evil.example")]
    [InlineData("/sales\\boxes")]
    public void A_backslash_is_the_other_slant_of_the_same_host_and_is_refused(string address)
    {
        Assert.False(WebAddress.IsNavigable(address, withinApp: true));
        Assert.False(WebAddress.IsNavigable(address, withinApp: false));
    }

    // Only where the surface says it admits this app's own addresses, and never the
    // protocol-relative "//host" that reads like one and names somebody else.
    [Theory]
    [InlineData("/sales/boxes", true)]
    [InlineData("/sales/boxes?open=1", true)]
    [InlineData("/", true)]
    [InlineData("//evil.example", false)]
    [InlineData("sales/boxes", false)]
    [InlineData("#section", false)]
    public void An_app_relative_address_is_navigable_only_where_the_surface_admits_one(string address, bool navigable)
    {
        Assert.Equal(navigable, WebAddress.IsNavigable(address, withinApp: true));
        Assert.False(WebAddress.IsNavigable(address, withinApp: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Nothing_is_not_an_address(string? address)
    {
        Assert.False(WebAddress.IsNavigable(address, withinApp: true));
    }
}
