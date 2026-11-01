// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A contributor's entry naming a Command instead of a destination: a row that runs an
/// act in place — the frame's Atajos de Teclado is the first — found by the drawer's search and
/// gated like any other entry, and the Stack's own Help group that holds it.</summary>
public sealed class NsNavMenuCommandEntryTests : BunitContext, IAsyncLifetime
{
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    CountingDialogManager Dialogs { get; } = new();

    IRenderedComponent<NsNavMenu> RenderMenu(Action<IServiceCollection> contribute)
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton<DialogManager>(Dialogs);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuCommandEntryTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(new CapturingMediator());

        contribute(Services);

        JSInterop.Mode = JSRuntimeMode.Loose;

        this.AddAuthorization().SetAuthorized("command-probe");

        return Render<NsNavMenu>();
    }

    IRenderedComponent<NsNavMenu> RenderMenu(params NavMenuItem[] entries)
    {
        return RenderMenu(services => services.AddSingleton<INavMenuContributor>(new FixedNavContributor(entries)));
    }

    [Fact]
    public void PressingTheEntryRunsItsCommand()
    {
        var runs = 0;

        var menu = RenderMenu(new NavMenuItem
        {
            Name = "Probe",
            Command = () =>
            {
                runs++;

                return Task.CompletedTask;
            }
        });

        menu.Find(".ns-nav-command .mud-nav-link").Click();

        Assert.Equal(1, runs);
    }

    // An act is not a place: nothing for the vendor to match against the address, and nothing a
    // middle click could open in a new window.
    [Fact]
    public void TheEntryCarriesNoAddress()
    {
        var menu = RenderMenu(new NavMenuItem { Name = "Probe", Command = () => Task.CompletedTask });

        Assert.Empty(menu.FindAll(".ns-nav-command a[href]"));
        Assert.Contains("Probe", menu.Find(".ns-nav-command").TextContent);
    }

    [Fact]
    public void TheSearchFindsItByItsLabel()
    {
        var menu = RenderMenu(
            new NavMenuItem { Name = "Probe", Command = () => Task.CompletedTask },
            new NavMenuItem { Name = "Other", Command = () => Task.CompletedTask });

        menu.Find("input").Input("prob");

        menu.WaitForAssertion(() =>
        {
            var rows = menu.FindAll(".ns-nav-entry");

            Assert.Single(rows);
            Assert.Contains("Probe", rows[0].TextContent);
        });
    }

    // The chord rides the control the click lands on, so the keyboard layer's activate() presses
    // exactly what a hand does.
    [Fact]
    public void ItsChordIsClaimedOnTheControlItRuns()
    {
        var menu = RenderMenu(new NavMenuItem
        {
            Name = "Probe",
            Command = () => Task.CompletedTask,
            Chord = NsKeys.Help
        });

        var control = menu.Find($".ns-nav-command [{NsKeys.Attribute}]");

        Assert.Equal("BUTTON", control.TagName);
        Assert.Equal("button", control.GetAttribute("type"));

        Assert.Equal(NsKeys.Help, control.GetAttribute(NsKeys.Attribute));
        Assert.Equal("Question", control.GetAttribute("aria-keyshortcuts"));
    }

    // The word is on screen and the key is not, so the row whispers both, as a chorded NsButton
    // does; a chordless row with its word on screen says nothing.
    [Fact]
    public void AChordedEntryWhispersItsWordAndKey()
    {
        var menu = RenderMenu(
            new NavMenuItem { Name = "Probe", Command = () => Task.CompletedTask, Chord = NsKeys.Help },
            new NavMenuItem { Name = "Other", Command = () => Task.CompletedTask });

        var rows = menu.FindAll(".ns-nav-command button");

        Assert.Equal($"Probe ({NsKeys.Help})", rows.Single(row => row.TextContent.Contains("Probe")).GetAttribute("data-ns-whisper"));
        Assert.Null(rows.Single(row => row.TextContent.Contains("Other")).GetAttribute("data-ns-whisper"));
    }

    [Fact]
    public void AHiddenCommandTakesItsChordWithIt()
    {
        var menu = RenderMenu(new NavMenuItem
        {
            Name = "Probe",
            Command = () => Task.CompletedTask,
            Chord = NsKeys.Help,
            Visible = false
        });

        Assert.Empty(menu.FindAll($"[{NsKeys.Attribute}=\"{NsKeys.Help}\"]"));
    }

    [Fact]
    public void TheSearchRowDrawsNoButtonOfTheFramesOwn()
    {
        var menu = RenderMenu(new NavMenuItem { Name = "Probe", Command = () => Task.CompletedTask });

        Assert.Empty(menu.FindAll(".ns-nav-find button"));
    }

    [Fact]
    public void TheHelpGroupHoldsTheShortcutsSheet()
    {
        var menu = RenderMenu(services => services.AddScoped<INavMenuContributor, HelpMenu>());

        Assert.Contains("Help", menu.Find(".mud-nav-group").TextContent);

        menu.Find(".ns-nav-command .mud-nav-link").Click();

        menu.WaitForAssertion(() => Assert.Equal(typeof(NsKeySheet), Assert.Single(Dialogs.Opens).Component));
    }

    // An app's own help lands under the Stack's group by naming it, after the sheet.
    [Fact]
    public async Task AnAppHangsItsOwnHelpUnderTheGroupAfterTheSheet()
    {
        RenderMenu(services =>
        {
            services.AddScoped<INavMenuContributor, HelpMenu>();
            services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
                new NavMenuItem { Name = "Guide", Parent = HelpMenu.Group, Url = "/guide", Weight = 10 }));
        });

        var merged = await Services.GetRequiredService<NavMenu>().GetItems(null);

        var group = Assert.Single(merged);

        Assert.Equal(["Shortcuts", "Guide"], group.Items.Select(item => item.Name));
        Assert.NotNull(group.Items[0].Command);
    }
}
