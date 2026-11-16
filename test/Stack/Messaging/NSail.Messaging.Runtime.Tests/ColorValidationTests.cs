// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Validation;

namespace NSail.Messaging.Runtime.Tests;

// A color a message carries is later painted into a style: a caller that skips NsColorField
// must not get text through that closes the declaration and opens another.
public sealed class ColorValidationTests
{
    sealed class Tinted
    {
        [Color]
        public string? Color { get; set; }
    }

    [Fact]
    public void AStyleInjection_IsRefused_NamingTheFieldAndTheCode()
    {
        var problem = MessageValidator.Validate(new Tinted { Color = "red;background:url(//x)" });

        Assert.NotNull(problem);
        Assert.Equal("InvalidModel", problem.Code);

        var issue = Assert.Single(problem.Issues!);

        Assert.Equal("InvalidFormat", issue.Code);
        Assert.Equal("Color", issue.Source);
    }

    [Theory]
    [InlineData("#1976d2")]
    [InlineData("rgb(25, 118, 210)")]
    [InlineData("rebeccapurple")]
    public void AColorThePickerProduces_Passes(string color)
    {
        Assert.Null(MessageValidator.Validate(new Tinted { Color = color }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnAbsentColor_Passes(string? color)
    {
        Assert.Null(MessageValidator.Validate(new Tinted { Color = color }));
    }
}
