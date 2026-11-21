// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The page name never gives way, and neither does the surface's width: where the
/// utilities outgrow even the line the title row gave them, they wrap among themselves at the
/// trailing edge instead of overflowing it. A group that cannot wrap has only overflow left
/// there, and that scrolls the whole surface sideways with the last utility cut at the edge.
/// bUnit lays out no box, so what a render proves is the row's shape on both faces — the
/// group's wrap and trailing justification, the outer row's wrap, and the title's lack of a
/// zero floor — and what it does to five utilities at 390 px is a browser walk's.
/// OverlayChromeTests holds the shell's own row, the third place this bar draws.</summary>
public sealed class NsTitleBarUtilitiesWrapTests : BunitContext, IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    // The overlay's X pulls in a MudBlazor service that is IAsyncDisposable-only, which bUnit's
    // synchronous teardown throws on.
    public new async Task DisposeAsync()
    {
        await ((IAsyncDisposable)this).DisposeAsync();
    }

    sealed class FakeJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return default;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return default;
        }
    }

    SurfaceContext Setup(Surface? name)
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Close"] = "Cerrar"
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton(new RouteTable(typeof(NsTitleBarUtilitiesWrapTests).Assembly, []));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var routes = Services.GetRequiredService<RouteTable>();
        var navigation = Services.GetRequiredService<NavigationManager>();

        return new SurfaceContext(name, navigation, routes, new FakeJs(), new SurfaceHistory());
    }

    static RenderFragment Utilities()
    {
        return builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "type", "button");
            builder.AddAttribute(2, "class", "probe-utility");
            builder.AddContent(3, "Imprimir 80 mm");
            builder.CloseElement();
        };
    }

    IRenderedComponent<NsTitleBar> Render(bool main)
    {
        var surface = Setup(main ? null : Surfaces.Aside);

        if (main)
        {
            return Render<MainSurfaceChromeHost>(p => p
                .Add(x => x.Surface, surface)
                .Add(x => x.Title, "Comprobante")
                .Add(x => x.Utilities, Utilities())).FindComponent<NsTitleBar>();
        }

        return Render<TitleBarSurfaceHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Comprobante")
            .Add(x => x.Utilities, Utilities())).FindComponent<NsTitleBar>();
    }

    static IElement Group(IRenderedComponent<NsTitleBar> bar)
    {
        return bar.Find(".probe-utility").ParentElement!;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheUtilitiesWrapAmongThemselvesAtTheTrailingEdge(bool main)
    {
        var group = Group(Render(main));

        Assert.Contains("ms-auto", group.ClassList);
        Assert.Contains("flex-wrap", group.ClassList);
        Assert.DoesNotContain("flex-nowrap", group.ClassList);
        Assert.Contains("justify-end", group.ClassList);
    }

    // The X is the group's last utility, so it wraps with the rest and stays the last one.
    [Fact]
    public void AnOverlaysCloseIsStillTheGroupsLastUtility()
    {
        var bar = Render(main: false);

        var last = Group(bar).LastElementChild!;

        Assert.Single(bar.FindComponents<NsClose>());
        Assert.Contains("Cerrar", last.OuterHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("probe-utility", last.OuterHtml, StringComparison.Ordinal);
    }

    // K2: the row still gives the utilities a line of their own first, and the title has no
    // zero floor to shrink to before the row considers wrapping.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheRowStillWrapsFirstAndTheTitleKeepsItsWidth(bool main)
    {
        var bar = Render(main);

        var row = Group(bar).ParentElement!;

        Assert.Contains("flex-wrap", row.ClassList);
        Assert.DoesNotContain("flex-nowrap", row.ClassList);

        var title = bar.Find(".mud-typography");

        Assert.DoesNotContain("text-truncate", title.ClassList);

        for (var box = title; box != row; box = box.ParentElement!)
        {
            Assert.DoesNotContain("min-w-0", box.ClassList);
        }
    }
}
