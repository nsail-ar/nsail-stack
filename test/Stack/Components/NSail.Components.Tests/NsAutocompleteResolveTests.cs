// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#857's seam. A lookup does two unrelated reads — the SEARCH the open dropdown
/// runs, and the RESOLVE an already-bound value asks for so the closed box can read as something
/// — and they used to share one Runner. Two jobs on one Runner is two ways to lose: a resolve
/// starting while a search is in flight threw ("Runner is already running") out of
/// OnParametersSetAsync, which the error boundary answers for by replacing the screen; and a
/// search starting while a resolve is in flight cancelled it, after the field had already
/// written the value down as asked — so the box stayed blank for that value for as long as it
/// was mounted.
///
/// Both are reached by exactly the gesture the report describes: a value arriving from OUTSIDE a
/// pick, which is what a create page's saved event is. The reads are slow on purpose — the
/// overlap IS the subject, and on localhost neither read is ever out long enough to meet the
/// other.</summary>
public sealed class NsAutocompleteResolveTests : BunitContext, IAsyncLifetime
{
    static readonly SelectRef Row = new(Guid.Parse("2f21f2a2-1a52-4f5a-8f3c-9c1b0d5a7e40"), "Taller Propio");

    static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(500);

    public NsAutocompleteResolveTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only, so bUnit's
    // synchronous teardown cannot dispose it (NsLookupCreateEntryTests' own note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static string Text(IRenderedComponent<GatedLookupHost> host)
    {
        return host.Find("input.mud-input-root").GetAttribute("value") ?? string.Empty;
    }

    /// <summary>The dropdown is open with its search still out — the state the field is in for
    /// as long as that read takes — when the create page's save assigns the row it made.</summary>
    [Fact]
    public async Task AValueArrivingWhileTheSearchIsStillOut_DoesNotFaultTheField()
    {
        var host = Render<GatedLookupHost>(p => p
            .Add(x => x.OnSearch, async args => { await Task.Delay(Latency, args.CancellationToken); args.Results = [Row]; })
            .Add(x => x.OnLoad, args => { args.Item = Row; return Task.CompletedTask; }));

        // Not awaited: the act this test is about happens WHILE this one is still out, which is
        // the whole subject. It is awaited at the foot so nothing is left unobserved.
        var opening = host.Find("input.mud-input-root").FocusAsync(new FocusEventArgs());

        host.Render(p => p.Add(x => x.Value, Row.Id));

        host.WaitForAssertion(() => Assert.Equal(Row.DisplayName, Text(host)));

        await opening;
    }

    /// <summary>The other order, and the one the report's blank box is: the resolve is out when
    /// something makes the field search again. A search may supersede a search; what it may not
    /// do is eat the answer to "what does the value I am showing read as" and leave the field
    /// with no way left to ask.</summary>
    [Fact]
    public async Task ASearchStartingWhileTheResolveIsOut_DoesNotLeaveTheFieldBlank()
    {
        var host = Render<GatedLookupHost>(p => p
            .Add(x => x.OnSearch, args => { args.Results = [Row]; return Task.CompletedTask; })
            .Add(x => x.OnLoad, async args => { await Task.Delay(Latency, args.CancellationToken); args.Item = Row; }));

        host.Render(p => p.Add(x => x.Value, Row.Id));

        await host.Find("input.mud-input-root").FocusAsync(new FocusEventArgs());

        host.WaitForAssertion(() => Assert.Equal(Row.DisplayName, Text(host)));
    }

    /// <summary>nsail#1218, the same window read for its chrome: for as long as the resolve is
    /// out the field holds a value it cannot yet read as anything, and a label at rest is a
    /// label standing where the row's name is about to be painted. It floats on the value, so
    /// the name lands under a label that is already up.</summary>
    [Fact]
    public void AValueWhoseResolveIsStillOut_AlreadyFloatsItsLabel()
    {
        var resolve = new TaskCompletionSource();

        var host = Render<GatedLookupHost>(p => p
            .Add(x => x.OnSearch, args => Task.CompletedTask)
            .Add(x => x.OnLoad, async args => { await resolve.Task; args.Item = Row; }));

        host.Render(p => p.Add(x => x.Value, Row.Id));

        Assert.Equal(string.Empty, Text(host));
        Assert.Contains("mud-shrink", host.Find("div.mud-input").ClassList);

        resolve.SetResult();

        host.WaitForAssertion(() => Assert.Equal(Row.DisplayName, Text(host)));
        Assert.Contains("mud-shrink", host.Find("div.mud-input").ClassList);
    }

    /// <summary>And the field nobody has answered keeps its label inside the box: the arm above
    /// is about a value, not about being a lookup.</summary>
    [Fact]
    public void ALookupNobodyAnswered_RestsItsLabelInTheBox()
    {
        var host = Render<GatedLookupHost>(p => p
            .Add(x => x.OnSearch, args => Task.CompletedTask)
            .Add(x => x.OnLoad, args => Task.CompletedTask));

        Assert.DoesNotContain("mud-shrink", host.Find("div.mud-input").ClassList);
    }
}
