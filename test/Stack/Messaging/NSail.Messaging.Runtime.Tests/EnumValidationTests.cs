// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Validation;

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
}
