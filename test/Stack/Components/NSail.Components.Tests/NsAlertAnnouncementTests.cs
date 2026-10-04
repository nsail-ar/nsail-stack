// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>nsail#1788: a Danger alert is read out as well as painted. The vendor marks none of
/// it, so a refusal that arrives with the page — the sign-in door's "that account already signs
/// in as another user" — is a block of colour to whoever cannot see it. Danger is the tone that
/// has to be heard before the person acts, and it is the only one marked: every other tone is
/// copy that sits on the screen, and a live region around static text announces nothing.
/// </summary>
public sealed class NsAlertAnnouncementTests : BunitContext
{
    public NsAlertAnnouncementTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
    }

    [Fact]
    public void ADangerAlertInterrupts()
    {
        var cut = Render<NsAlert>(p => p
            .Add(x => x.As, NsAs.Danger)
            .AddChildContent("No pudimos completar el ingreso."));

        var alert = cut.Find(".mud-alert");

        Assert.Equal("alert", alert.GetAttribute("role"));
        Assert.Contains("No pudimos completar el ingreso.", alert.TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NsAs.Default)]
    [InlineData(NsAs.Important)]
    [InlineData(NsAs.Main)]
    public void EveryOtherToneIsLeftUnmarked(NsAs tone)
    {
        var cut = Render<NsAlert>(p => p
            .Add(x => x.As, tone)
            .AddChildContent("Listo."));

        Assert.Null(cut.Find(".mud-alert").GetAttribute("role"));
    }
}
