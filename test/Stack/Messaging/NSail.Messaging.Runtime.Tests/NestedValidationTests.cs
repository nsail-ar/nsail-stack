// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Messaging.Runtime.Tests;

// The reach a message grants over its own parts: MessageValidator walks the members a message
// declares, and [Validated] takes that same walk into the member's model — the annotations, the
// codes and the IValidatableObject pass, with the field named by its path so a form can still
// find the input to draw under. A member that grants nothing is walked by nobody, which is what
// keeps a row model out of a rule neither end enforces.
public sealed class NestedValidationTests
{
    sealed class Eye
    {
        [Range(-30, 30)]
        public decimal Sphere { get; set; }

        [Required]
        public string? Note { get; set; }
    }

    sealed class Coded : ValidationAttribute, ICodedValidation
    {
        public string Code
        {
            get { return "SphereRange"; }
        }

        public IReadOnlyDictionary<string, string>? Arguments
        {
            get { return new Dictionary<string, string> { ["from"] = "-30.00", ["to"] = "30.00" }; }
        }

        public override bool IsValid(object? value)
        {
            return value is not decimal number || number is >= -30m and <= 30m;
        }
    }

    sealed class CodedEye
    {
        [Coded]
        public decimal Sphere { get; set; }
    }

    sealed class Prescription
    {
        [Validated]
        public Eye Right { get; set; } = new() { Note = "ok" };

        [Validated]
        public Eye? Left { get; set; }

        public Eye Loose { get; set; } = new();
    }

    sealed class Coding
    {
        [Validated]
        public CodedEye Right { get; set; } = new();
    }

    sealed class Ring
    {
        [Validated]
        public Ring? Next { get; set; }

        [Range(0, 1)]
        public int Number { get; set; }
    }

    sealed class Rule : IValidatableObject
    {
        public int Number { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Number != 0)
            {
                yield return new ValidationResult("The number is wrong.", [nameof(Number)]);
            }
        }
    }

    sealed class Ruled
    {
        [Validated]
        public Rule Inner { get; set; } = new();
    }

    [Fact]
    public void AMemberThatOpensItsModel_IsRefusedByThePathFromTheMessage()
    {
        var problem = MessageValidator.Validate(new Prescription { Right = new Eye { Sphere = 45m, Note = "ok" } });

        var issue = Assert.Single(problem!.Issues);

        Assert.Equal("OutOfRange", issue.Code);
        Assert.Equal("Right.Sphere", issue.Source);
    }

    // Being a property of a message is not the grant: only the member that says [Validated] is
    // walked, so a row model keeps the scope it had.
    [Fact]
    public void AMemberThatOpensNothing_IsNotWalked()
    {
        var problem = MessageValidator.Validate(new Prescription { Loose = new Eye { Sphere = 45m } });

        Assert.Null(problem);
    }

    [Fact]
    public void EveryOpenedMember_Answers()
    {
        var problem = MessageValidator.Validate(new Prescription
        {
            Right = new Eye { Sphere = 45m, Note = "ok" },
            Left = new Eye { Sphere = -45m, Note = "ok" },
        });

        Assert.Equal(
            ["Right.Sphere", "Left.Sphere"],
            problem!.Issues.Select(issue => issue.Source));
    }

    [Fact]
    public void AnOpenedMemberHoldingNothing_RefusesNothing()
    {
        Assert.Null(MessageValidator.Validate(new Prescription()));
    }

    [Fact]
    public void ARequirementInsideAnOpenedModel_IsRefusedLikeTheMessagesOwn()
    {
        var problem = MessageValidator.Validate(new Prescription { Right = new Eye() });

        var issue = Assert.Single(problem!.Issues);

        Assert.Equal("Required", issue.Code);
        Assert.Equal("Right.Note", issue.Source);
    }

    // The whole point of the reach: a coded attribute nested one level deep still mints the
    // code and the bound its sentence quotes.
    [Fact]
    public void ACodedRefusalInsideAnOpenedModel_CarriesItsCodeAndItsArguments()
    {
        var problem = MessageValidator.Validate(new Coding { Right = new CodedEye { Sphere = 45m } });

        var issue = Assert.Single(problem!.Issues);

        Assert.Equal("SphereRange", issue.Code);
        Assert.Equal("Right.Sphere", issue.Source);
        Assert.Equal("-30.00", issue.Arguments!["from"]);
        Assert.Equal("30.00", issue.Arguments["to"]);
        Assert.Equal("Right.Sphere", issue.Arguments["field"]);
    }

    [Fact]
    public void ARuleInsideAnOpenedModel_NamesItsMemberByThePath()
    {
        var problem = MessageValidator.Validate(new Ruled { Inner = new Rule { Number = 1 } });

        Assert.Equal("Inner.Number", Assert.Single(problem!.Issues).Source);
    }

    // Two models opening each other terminate: every model is walked once.
    [Fact]
    public void ARingOfOpenedMembers_Terminates()
    {
        var first = new Ring { Number = 5 };
        var second = new Ring { Number = 5, Next = first };

        first.Next = second;

        var problem = MessageValidator.Validate(first);

        Assert.Equal(["Next.Number", "Number"], problem!.Issues.Select(issue => issue.Source));
    }
}
