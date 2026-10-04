// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1815: "cuando apretás Nuevo estaría bueno darle foco al primer campo" — a
/// create screen's first field takes focus without a page writing a line for it, derived two
/// ways: the message's own Create* name (messaging.md), and a tracked form handed its model
/// directly with no OnLoad behind it — the newborn document itself, whatever its name
/// (nsail#1815 round 3). Either one is enough, and an edit screen is never a candidate for
/// either.</summary>
public sealed class NsFormCreateFocusTests : BunitContext, IAsyncLifetime
{
    const string Signal = "nsapp.focusFirst";

    public NsFormCreateFocusTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IReadOnlyList<JSRuntimeInvocation> Calls => [.. JSInterop.Invocations[Signal]];

    [Fact]
    public void ACreateNamedFormWithAModelHandedIn_FocusesItsFirstField()
    {
        Render<AutofocusFormHost<CreateFocusProbe>>();

        Assert.Single(Calls);
    }

    // The naming convention's own false negative, caught structurally instead: a message
    // outside the closed verb set (RegisterClient, OpenTicket — naming.md) still mints a
    // document when it is handed to a tracked form directly, with nothing read behind it.
    [Fact]
    public void ANonCreateNamedFormWithAModelHandedInDirectly_FocusesItsFirstFieldToo()
    {
        Render<AutofocusFormHost<UpdateFocusProbe>>();

        Assert.Single(Calls);
    }

    // The actual edit shape every update screen in the tree uses: a document read through
    // OnLoad, named for what it is. Neither signal fires, and nothing overrides them.
    [Fact]
    public async Task AnEditFormReadThroughOnLoad_IsNeverFocused()
    {
        var host = Render<AutofocusLoadFormHost<UpdateFocusProbe>>();

        await host.InvokeAsync(host.Instance.Answer);

        Assert.Empty(Calls);
    }

    // An Untracked form reads as "holds no document" to the structural signal, which is right
    // for a filter or an act but wrong for a form that still mints one in a dialog
    // (RegisterPractitionerForm) — FocusFirstField is the override that one reaches for.
    [Fact]
    public void AnUntrackedNonCreateNamedFormWithAModelHandedInDirectly_IsNeverFocusedByDefault()
    {
        Render<AutofocusFormHost<UpdateFocusProbe>>(p => p
            .Add(h => h.Untracked, true));

        Assert.Empty(Calls);
    }

    [Fact]
    public void AnUntrackedFormOverriddenOn_FocusesItsFirstFieldAnyway()
    {
        Render<AutofocusFormHost<UpdateFocusProbe>>(p => p
            .Add(h => h.Untracked, true)
            .Add(h => h.FocusFirstField, true));

        Assert.Single(Calls);
    }

    [Fact]
    public async Task ACreateFormWhoseModelArrivesThroughOnLoad_FocusesOnceTheReadAnswers()
    {
        var host = Render<AutofocusLoadFormHost<CreateFocusProbe>>();

        Assert.Empty(Calls);

        await host.InvokeAsync(host.Instance.Answer);

        host.WaitForAssertion(() => Assert.Single(Calls));
    }

    [Fact]
    public async Task ASuccessfulSubmitThatReloadsTheSameCreateForm_IsNotFocusedAgain()
    {
        var host = Render<AutofocusLoadFormHost<CreateFocusProbe>>();

        await host.InvokeAsync(host.Instance.Answer);

        host.WaitForAssertion(() => Assert.Single(Calls));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Equal(1, host.Instance.Submits);
        Assert.Single(Calls);
    }

    // nsail#1815 round 2 (Vigía): a Create*-shaped model reused to EDIT something that already
    // exists (UpdatePartyPage locks Kind and reads through OnLoad) is the naming convention's own
    // false positive — FocusFirstField is the override a page reaches for instead.
    [Fact]
    public async Task ACreateFormOverriddenOff_IsNeverFocusedEvenAfterItsLoadAnswers()
    {
        var host = Render<AutofocusLoadFormHost<CreateFocusProbe>>(p => p
            .Add(h => h.FocusFirstField, false));

        await host.InvokeAsync(host.Instance.Answer);

        Assert.Empty(Calls);
    }
}
