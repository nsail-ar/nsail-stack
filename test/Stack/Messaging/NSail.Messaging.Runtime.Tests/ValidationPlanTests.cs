// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Messaging.Runtime.Tests;

public sealed class ValidationPlanTests
{
    // The runtime builds a fresh attribute instance on every reflective read, so the
    // constructions count the reads.
    sealed class CountedAttribute : ValidationAttribute
    {
        public static int Built;

        public CountedAttribute()
        {
            Interlocked.Increment(ref Built);
        }

        public override bool IsValid(object? value)
        {
            return value is not null;
        }
    }

    sealed class Counted
    {
        [Counted]
        public string? Name { get; set; }
    }

    [Fact]
    public void A_thousand_sends_of_one_type_read_its_attributes_once()
    {
        Assert.Null(MessageValidator.Validate(new Counted { Name = "a" }));
        var afterFirst = CountedAttribute.Built;

        for (var i = 0; i < 1000; i++)
        {
            Assert.Null(MessageValidator.Validate(new Counted { Name = "a" }));
        }

        Assert.Equal(afterFirst, CountedAttribute.Built);
        Assert.NotNull(MessageValidator.Validate(new Counted()));
    }
}
