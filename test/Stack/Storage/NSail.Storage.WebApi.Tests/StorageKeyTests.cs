// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;
using NSail.Storage.Objects;

namespace NSail.Storage.Tests;

// StorageManager.Guard is the one door every key comes through, so what it lets past is what
// every provider — including the one that turns a key into a path — has to survive.
public sealed class StorageKeyTests
{
    [Theory]
    [InlineData("backups/2026-08-21.zip", "backups/2026-08-21.zip")]
    [InlineData("/backups/a.zip", "backups/a.zip")]
    [InlineData("backups\\a.zip", "backups/a.zip")]
    [InlineData("  backups/a.zip  ", "backups/a.zip")]
    public void KeepsAKeyThatNamesSomethingInsideTheContainer(string key, string expected)
    {
        Assert.Equal(expected, StorageManager.Guard(key));
    }

    [Theory]
    [InlineData("../escaped.zip")]
    [InlineData("backups/../../escaped.zip")]
    [InlineData("backups/./a.zip")]
    [InlineData("backups//a.zip")]
    public void RefusesAKeyThatCouldLandOutsideIt(string key)
    {
        Assert.Throws<BusinessException>(() => StorageManager.Guard(key));
    }

    // Not a refusal but a caller bug: nothing a user typed produces an empty key, so it is
    // answered as the argument error it is rather than dressed up as a rule the store imposes.
    [Fact]
    public void RefusesAnEmptyKeyAsTheArgumentErrorItIs()
    {
        Assert.Throws<ArgumentException>(() => StorageManager.Guard("   "));
    }
}
