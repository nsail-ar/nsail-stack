// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Dates;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Messaging.Runtime.Tests;

// The seam that lets a house attribute be worded from the catalog at both ends: MessageValidator
// mints the Issue the wire carries, and a code of the attribute's own is what a screen and a
// server refusal both resolve ("Problems.{Code}.{field}", then "Problems.{Code}"). Without it an
// attribute outside the BCL vocabulary lands on the generic "Invalid" and its raw ErrorMessage
// reaches the eye in whatever language it was written in.
public sealed class CodedValidationTests
{
    sealed class Birth
    {
        [NotFuture]
        public DateOnly? On { get; set; }
    }

    sealed class Moment
    {
        [NotFuture]
        public DateTime? At { get; set; }
    }

    sealed class Zoned
    {
        [NotFuture]
        public DateTimeOffset? At { get; set; }
    }

    sealed class Sized
    {
        [MaxLength(3)]
        public string? Code { get; set; }
    }

    [Fact]
    public void NoDate_Passes()
    {
        Assert.Null(MessageValidator.Validate(new Birth { On = null }));
    }

    [Fact]
    public void Today_Passes()
    {
        Assert.Null(MessageValidator.Validate(new Birth { On = BusinessDate.Today }));
    }

    [Fact]
    public void Yesterday_Passes()
    {
        Assert.Null(MessageValidator.Validate(new Birth { On = BusinessDate.Today.AddDays(-1) }));
    }

    [Fact]
    public void Tomorrow_IsRefused_NamingTheFieldAndTheCode()
    {
        var problem = MessageValidator.Validate(new Birth { On = BusinessDate.Today.AddDays(1) });

        Assert.NotNull(problem);
        Assert.Equal("InvalidModel", problem.Code);

        var issue = Assert.Single(problem.Issues!);

        Assert.Equal("NotFuture", issue.Code);
        Assert.Equal("On", issue.Source);
    }

    [Fact]
    public void ADateTime_LaterToday_Passes_AndTomorrowDoesNot()
    {
        var midnight = BusinessDate.Today.ToDateTime(TimeOnly.MinValue);

        Assert.Null(MessageValidator.Validate(new Moment { At = midnight.AddHours(23) }));
        Assert.NotNull(MessageValidator.Validate(new Moment { At = midnight.AddDays(1) }));
    }

    [Fact]
    public void ADateTimeOffset_IsReadOnTheHostsOwnCalendar()
    {
        var midnight = BusinessDate.Today.ToDateTime(TimeOnly.MinValue);

        Assert.Null(MessageValidator.Validate(new Zoned { At = new DateTimeOffset(midnight) }));
        Assert.NotNull(MessageValidator.Validate(new Zoned { At = new DateTimeOffset(midnight.AddDays(1)) }));
    }

    // Non-vacuity for the switch arm's placement: an attribute that is NOT coded keeps landing
    // on the vocabulary it already had.
    [Fact]
    public void AnUncodedAttribute_KeepsItsOwnCode()
    {
        var problem = MessageValidator.Validate(new Sized { Code = "abcd" });

        Assert.NotNull(problem);

        var issue = Assert.Single(problem.Issues!);

        Assert.Equal("MaxLength", issue.Code);
        Assert.Equal("Code", issue.Source);
    }

    [Fact]
    public void TheGuard_ThrowsOnAFutureDate_AndSaysNothingOnAPastOne()
    {
        MessageValidator.Guard(new Birth { On = BusinessDate.Today });

        var exception = Assert.Throws<BusinessException>(
            () => MessageValidator.Guard(new Birth { On = BusinessDate.Today.AddDays(1) }));

        Assert.Equal("InvalidModel", exception.Code);
        Assert.Equal(400, exception.Status);
    }
}
