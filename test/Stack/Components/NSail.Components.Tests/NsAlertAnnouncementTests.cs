// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>nsail#1788: an Error or Warning alert is read out as well as painted. The vendor
/// marks none of it, so a refusal that arrives with the page — the sign-in door's "that account
/// already signs in as another user" — is a block of colour to whoever cannot see it. Those are
/// the tones that have to be heard before the person acts, and the only ones marked: Info and
/// Success are copy that sits on the screen, and a live region around static text announces
/// nothing. Each severity draws its own vendor colour, so an Error is red.</summary>
public sealed class NsAlertAnnouncementTests : BunitContext
{
    public NsAlertAnnouncementTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
    }

    [Theory]
    [InlineData(NsSeverity.Error, "mud-alert-filled-error", "alert")]
    [InlineData(NsSeverity.Warning, "mud-alert-filled-warning", "alert")]
    [InlineData(NsSeverity.Success, "mud-alert-filled-success", null)]
    [InlineData(NsSeverity.Info, "mud-alert-filled-info", null)]
    public void EachSeverityDrawsItsOwnColour_andOnlyErrorAndWarningInterrupt(NsSeverity severity, string colour, string? role)
    {
        var cut = Render<NsAlert>(p => p
            .Add(x => x.Severity, severity)
            .AddChildContent("No pudimos completar el ingreso."));

        var alert = cut.Find(".mud-alert");

        Assert.Contains(colour, alert.ClassList);
        Assert.Equal(role, alert.GetAttribute("role"));
        Assert.Contains("No pudimos completar el ingreso.", alert.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAlertThatSaysNothing_isInfo()
    {
        var cut = Render<NsAlert>(p => p.AddChildContent("Listo."));

        var alert = cut.Find(".mud-alert");

        Assert.Contains("mud-alert-filled-info", alert.ClassList);
        Assert.Null(alert.GetAttribute("role"));
    }
}
