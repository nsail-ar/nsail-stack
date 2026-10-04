// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security.Tests;

// nsail#1754: SignInPage, BookingPage and the OAuth *State classes all round-trip a
// returnUrl/returnTo an anonymous caller controls, so the guard that only a same-origin
// path survives lives once, here, rather than as three (or five) copies of the same shape.
public class SafeReturnToTests
{
    [Theory]
    [InlineData("https://evil.test/steal")]
    [InlineData("//evil.test/steal")]
    [InlineData("directory/parties")]
    [InlineData(null)]
    [InlineData("")]
    // A backslash reads as authority-state to a browser's URL parser, so "/\evil.test"
    // resolves to https://evil.test despite the leading single slash.
    [InlineData("/\\evil.test")]
    // A tab is stripped before the parser looks at the next character, so "/\t/evil.test"
    // resolves the same way once the control character is gone.
    [InlineData("/\t/evil.test")]
    public void AnythingThatCouldLeaveThisInstallIsRejected(string? value)
    {
        Assert.Null(SafeReturnTo.Parse(value));
        Assert.Equal("/", SafeReturnTo.Or(value));
    }

    [Theory]
    [InlineData("/directory/parties")]
    [InlineData("/directory/parties?x=1")]
    public void ASameOriginPathIsHonoured(string value)
    {
        Assert.Equal(value, SafeReturnTo.Parse(value));
        Assert.Equal(value, SafeReturnTo.Or(value));
    }
}
