// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Messaging.Runtime.Tests;

// The numeric bounds the Stack owns beside [NotFuture], and the seam a cross-field rule needs to
// exist on the wire at all: MessageValidator asked attribute.IsValid(value), the overload that
// carries no model, so a rule about two members could not be written for the wire — it reads
// the second member off a context that overload never has. The screen has always validated with
// one, and a bound only one end can make is nsail#820's defect wearing a different hat.
public sealed class BoundValidationTests
{
    sealed class Priced
    {
        [NotNegative]
        public decimal Price { get; set; }

        [NotNegative]
        public decimal? MinimumStock { get; set; }

        [NotNegative]
        public int Count { get; set; }

        [NotNegative]
        public string? Name { get; set; }
    }

    sealed class Charged
    {
        [PositiveAmount]
        public decimal Price { get; set; }

        [PositiveAmount]
        public decimal? Discount { get; set; }

        [PositiveAmount]
        public int Units { get; set; }
    }

    sealed class Span
    {
        [NotAbove(nameof(To))]
        public decimal? From { get; set; }

        public decimal? To { get; set; }
    }

    sealed class Misnamed
    {
        [NotAbove("Nowhere")]
        public decimal? From { get; set; }
    }

    sealed class Mismatched
    {
        [NotAbove(nameof(To))]
        public decimal? From { get; set; }

        public int? To { get; set; }
    }

    [Fact]
    public void ZeroIsNotNegative()
    {
        Assert.Null(MessageValidator.Validate(new Priced { Price = 0m, MinimumStock = 0m }));
    }

    [Fact]
    public void AnAbsentValueDeclaresNothingToRefuse()
    {
        Assert.Null(MessageValidator.Validate(new Priced { MinimumStock = null }));
    }

    [Fact]
    public void TheReportedPriceAndMinimumStockAreRefusedTogether()
    {
        var problem = MessageValidator.Validate(new Priced { Price = -100m, MinimumStock = -5m });

        Assert.NotNull(problem);
        Assert.Equal("InvalidModel", problem.Code);

        Assert.Equal(
            [("NotNegative", "Price"), ("NotNegative", "MinimumStock")],
            problem.Issues!.Select(issue => (issue.Code, issue.Source)));
    }

    [Fact]
    public void AnIntegerIsRefusedByTheSameRule()
    {
        var problem = MessageValidator.Validate(new Priced { Count = -1 });

        Assert.Equal("NotNegative", Assert.Single(problem!.Issues!).Code);
    }

    // What is not a number is not this rule's business: the attribute answers for the shapes a
    // numeric field binds and leaves everything else to whatever declares it.
    [Fact]
    public void ATextMemberIsNotThisRulesBusiness()
    {
        Assert.Null(MessageValidator.Validate(new Priced { Name = "-100" }));
    }

    // nsail#1622: the price cell's floor used to be [Range(0.01, double.MaxValue)], which no
    // catalog can word — the screen drew "The field Price must be between 0,01 and
    // 1,79769…E+308" in English. The bound is the same one, named by its own code.
    [Theory]
    [InlineData(0.01)]
    [InlineData(1500.0)]
    public void AnAmountOfAtLeastOneCentPasses(double price)
    {
        Assert.Null(MessageValidator.Validate(new Charged { Price = (decimal)price, Units = 1 }));
    }

    // The floor is the cent rather than zero itself: money is kept to two decimals, so 0,004 is
    // a figure the box draws as "0,00" and the column stores as nothing.
    [Theory]
    [InlineData(-100.0)]
    [InlineData(0.0)]
    [InlineData(0.004)]
    public void AnAmountOfNothingIsRefusedByItsOwnCode(double price)
    {
        var problem = MessageValidator.Validate(new Charged { Price = (decimal)price, Units = 1 });

        Assert.NotNull(problem);

        var issue = Assert.Single(problem.Issues!);

        Assert.Equal("PositiveAmount", issue.Code);
        Assert.Equal(nameof(Charged.Price), issue.Source);
    }

    [Fact]
    public void AnAbsentAmountDeclaresNothingToRefuse()
    {
        Assert.Null(MessageValidator.Validate(new Charged { Price = 1m, Discount = null, Units = 1 }));
    }

    // An amount held in a whole number has no cent to reach, so its own smallest step is the
    // floor — zero units is still an amount of nothing.
    [Fact]
    public void AWholeAmountIsRefusedAtZeroAndPassesAtOne()
    {
        Assert.Equal("PositiveAmount", Assert.Single(MessageValidator.Validate(new Charged { Price = 1m, Units = 0 })!.Issues!).Code);
        Assert.Null(MessageValidator.Validate(new Charged { Price = 1m, Units = 1 }));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(-2.0, null)]
    [InlineData(null, 2.0)]
    [InlineData(-2.0, 2.0)]
    [InlineData(2.0, 2.0)]
    public void APairInOrderOrHalfDeclaredPasses(double? from, double? to)
    {
        Assert.Null(MessageValidator.Validate(new Span
        {
            From = (decimal?)from,
            To = (decimal?)to,
        }));
    }

    /// <summary>The refusal the wire could not make before: the rule reads the second member off
    /// the model, which only the context-carrying overload hands it.</summary>
    [Fact]
    public void ADesdeAboveItsHastaIsRefusedOnTheWire()
    {
        var problem = MessageValidator.Validate(new Span { From = 4m, To = 2m });

        Assert.NotNull(problem);

        var issue = Assert.Single(problem.Issues!);

        Assert.Equal("NotAbove", issue.Code);
        Assert.Equal("From", issue.Source);
    }

    [Fact]
    public void AMemberTheAttributeCannotFindSaysSoRatherThanPassing()
    {
        Assert.Throws<InvalidOperationException>(() => MessageValidator.Validate(new Misnamed { From = 1m }));
    }

    [Fact]
    public void APairOfDifferentShapesSaysSoRatherThanPassing()
    {
        Assert.Throws<InvalidOperationException>(
            () => MessageValidator.Validate(new Mismatched { From = 4m, To = 2 }));
    }

    /// <summary>The untranslated fallback a client with no catalog reads names both members, so
    /// a refusal nobody translated still says which pair it is about.</summary>
    [Fact]
    public void TheFallbackSentenceNamesBothMembers()
    {
        Assert.Equal(
            "The field From cannot be greater than To.",
            new NotAboveAttribute("To").FormatErrorMessage("From"));

        Assert.Equal(
            "The field Price cannot be below zero.",
            new NotNegativeAttribute().FormatErrorMessage("Price"));
    }

    /// <summary>Non-vacuity for the context change: the BCL vocabulary is walked exactly as
    /// before, and a member declaring nothing is still walked by nobody.</summary>
    [Fact]
    public void TheBclVocabularyIsUnchangedByTheContext()
    {
        var problem = MessageValidator.Validate(new Sized { Code = "abcd", Name = null });

        Assert.NotNull(problem);

        Assert.Equal(
            [("MaxLength", "Code"), ("Required", "Name")],
            problem.Issues!.Select(issue => (issue.Code, issue.Source)));
    }

    sealed class Sized
    {
        [MaxLength(3)]
        public string? Code { get; set; }

        [Required]
        public string? Name { get; set; }

        public string? Free { get; set; }
    }
}
