// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>nsail#137, ruled 2026-08-21: NsContainer stacks by default — two blocks inside it
/// land one above the other, and a caller that wants a row asks for it explicitly. Before this,
/// NsContainer applied "d-flex" with no "flex-column", so SignInPage's provider buttons landed
/// beside the sign-in card instead of below it the moment a second provider contributed.</summary>
public sealed class NsContainerDirectionTests : BunitContext
{
    public NsContainerDirectionTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
    }

    [Fact]
    public void ByDefault_NsContainerStacksItsChildrenInAColumn()
    {
        var cut = Render<NSail.Components.NsContainer>(p => p
            .AddChildContent("<div>one</div><div>two</div>"));

        var container = cut.Find(".mud-container");

        Assert.Contains("d-flex", container.ClassList);
        Assert.Contains("flex-column", container.ClassList);
    }

    [Fact]
    public void Horizontal_dropsTheColumnDirection_forACallerThatAsksForARowExplicitly()
    {
        var cut = Render<NSail.Components.NsContainer>(p => p
            .Add(x => x.Horizontal, true)
            .AddChildContent("<div>one</div><div>two</div>"));

        var container = cut.Find(".mud-container");

        Assert.Contains("d-flex", container.ClassList);
        Assert.DoesNotContain("flex-column", container.ClassList);
    }
}
