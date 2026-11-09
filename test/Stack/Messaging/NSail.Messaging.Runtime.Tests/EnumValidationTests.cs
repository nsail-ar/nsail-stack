// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Validation;
using NSail.Paging;

namespace NSail.Messaging.Runtime.Tests;

public sealed class EnumValidationTests
{
    enum Kind { A = 1, B = 2 }

    [Flags]
    enum Bits { None = 0, X = 1, Y = 4 }

    sealed class Holder
    {
        public Kind Kind { get; set; } = Kind.A;

        public Kind? Maybe { get; set; }

        public Bits Bits { get; set; }

        public List<Kind>? Kinds { get; set; }
    }

    sealed class Listing : PagedMessage
    {
    }

    [Fact]
    public void UndefinedNumber_IsRefused_NamingTheField()
    {
        var problem = MessageValidator.Validate(new Holder { Kind = (Kind)99 });

        var issue = Assert.Single(problem!.Issues!);
        Assert.Equal("OutOfRange", issue.Code);
        Assert.Equal("Kind", issue.Source);
    }

    [Fact]
    public void UndefinedNullable_IsRefused()
    {
        Assert.NotNull(MessageValidator.Validate(new Holder { Maybe = (Kind)7 }));
    }

    [Fact]
    public void DefinedAndAbsent_Pass()
    {
        Assert.Null(MessageValidator.Validate(new Holder { Kind = Kind.B, Maybe = Kind.A }));
    }

    [Fact]
    public void Flags_CombinationPasses_StrayBitIsRefused()
    {
        Assert.Null(MessageValidator.Validate(new Holder { Bits = Bits.X | Bits.Y }));
        Assert.NotNull(MessageValidator.Validate(new Holder { Bits = (Bits)2 }));
    }

    [Fact]
    public void UndefinedElement_IsRefused_NamingTheIndex()
    {
        var problem = MessageValidator.Validate(new Holder { Kinds = [Kind.A, (Kind)99] });

        var issue = Assert.Single(problem!.Issues!);
        Assert.Equal("Kinds[1]", issue.Source);
        Assert.Null(MessageValidator.Validate(new Holder { Kinds = [Kind.A, Kind.B] }));
    }

    [Fact]
    public void PageIndex_PastTheOffsetBound_IsRefused()
    {
        Assert.NotNull(MessageValidator.Validate(new Listing { PageIndex = int.MaxValue, PageSize = 1000 }));
        Assert.Null(MessageValidator.Validate(new Listing { PageIndex = PagedMessage.MaxPageIndex, PageSize = 1000 }));
    }
}
