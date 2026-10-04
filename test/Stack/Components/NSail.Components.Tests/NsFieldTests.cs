// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>NsField is the field chrome alone, for a field the user operates through composed
/// controls (NsImageField: a preview beside a picker) rather than one input. The design
/// decision worth pinning is the one that removes a way to get it wrong: the problem text is
/// the error state, so there is no second flag to disagree with it.</summary>
public sealed class NsFieldTests : BunitContext
{
    public NsFieldTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void NsField_ShowsItsLabelAndContent()
    {
        var cut = Render<NsField>(ps => ps
            .Add(p => p.Label, "Logo")
            .AddChildContent("<span id=\"slot\">picked.svg</span>"));

        Assert.Contains("Logo", cut.Markup);
        Assert.Equal("picked.svg", cut.Find("#slot").TextContent);
    }

    /// <summary>Null text is no problem; text is a problem. One parameter drives both, which
    /// is why a caller cannot set an error that renders nothing.</summary>
    [Fact]
    public void NsField_TakesItsErrorStateFromTheProblemText()
    {
        var quiet = Render<NsField>(ps => ps.Add(p => p.Label, "Logo"));

        Assert.Empty(quiet.FindAll(".mud-input-error"));

        var complaining = Render<NsField>(ps => ps
            .Add(p => p.Label, "Logo")
            .Add(p => p.ErrorText, "El archivo es demasiado grande."));

        Assert.Contains("El archivo es demasiado grande.", complaining.Markup);
        Assert.NotEmpty(complaining.FindAll(".mud-input-error"));
    }

    /// <summary>The refusal reaches an assistive technology here too, and it has to be reached
    /// differently: this field drives no input of its own, so the box IS the described element
    /// and the reference rides it. The words themselves live in a node no eye sees, named by the
    /// box — the same shape every other field uses, hung off the only element the house owns
    /// when the controls inside are the caller's (nsail#216).</summary>
    [Fact]
    public void NsField_NamesItsProblemToAScreenReader()
    {
        var quiet = Render<NsField>(ps => ps.Add(p => p.Label, "Logo"));

        Assert.Null(quiet.Find(".mud-input-control").GetAttribute("aria-describedby"));
        Assert.Empty(quiet.FindAll(".ns-field-described"));

        var complaining = Render<NsField>(ps => ps
            .Add(p => p.Label, "Logo")
            .Add(p => p.ErrorText, "El archivo es demasiado grande."));

        var named = complaining.Find(".mud-input-control").GetAttribute("aria-describedby");

        Assert.False(string.IsNullOrWhiteSpace(named));

        var described = complaining.Find($"#{named}");

        Assert.Contains("ns-field-described", described.ClassList);
        Assert.Equal("El archivo es demasiado grande.", described.TextContent);
    }
}
