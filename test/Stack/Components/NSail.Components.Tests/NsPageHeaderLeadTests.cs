// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1475: the toolbar's leading zone is not the search box. A control that is not
/// a search — the assignee of a thread — stands there at its own width, and it does not spend
/// the search slot to do it: a screen that wants both keeps both.</summary>
public sealed class NsPageHeaderLeadTests : BunitContext, IAsyncLifetime
{
    public NsPageHeaderLeadTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>())]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton(new RouteTable(typeof(ProbeGuidePage).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static RenderFragment Marked(string mark)
    {
        return builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", mark);
            builder.CloseElement();
        };
    }

    // The width rule the search box wears (.ns-toolbar-search, flex 0 1 22rem) is the search
    // box's own: a lead that inherited it would be sized by a box it is not.
    [Fact]
    public void TheLeadStandsInTheToolbarAndOutsideTheSearchBoxsOwnWidth()
    {
        var cut = Render<NsPageHeader>(parameters => parameters
            .Add(header => header.Title, "Conversación")
            .Add(header => header.Lead, Marked("probe-lead")));

        Assert.Empty(cut.FindAll(".ns-toolbar-search"));
        Assert.Single(cut.FindAll(".probe-lead"));
    }

    // A header with nothing else in its toolbar still draws one for it, the way a lone Actions
    // fragment does — otherwise the lead would be dropped on a screen that lists nothing.
    [Fact]
    public void ALeadIsEnoughToDrawTheToolbar()
    {
        var cut = Render<NsPageHeader>(parameters => parameters
            .Add(header => header.Title, "Conversación")
            .Add(header => header.Lead, Marked("probe-lead")));

        Assert.Single(cut.FindComponents<NsToolBar>());
    }

    // The two zones are not the same zone: a list with a scope selector on the left can still
    // put a control before it, and the search keeps its own box.
    [Fact]
    public void ALeadDoesNotSpendTheSearchSlot()
    {
        var cut = Render<NsPageHeader>(parameters => parameters
            .Add(header => header.Title, "Conversaciones")
            .Add(header => header.Lead, Marked("probe-lead"))
            .Add(header => header.Search, Marked("probe-search")));

        var search = cut.Find(".ns-toolbar-search");

        Assert.Single(search.QuerySelectorAll(".probe-search"));
        Assert.Empty(search.QuerySelectorAll(".probe-lead"));
        Assert.Single(cut.FindAll(".probe-lead"));
    }
}
