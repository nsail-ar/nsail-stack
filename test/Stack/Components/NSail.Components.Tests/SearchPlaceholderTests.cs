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

/// <summary>Every box the Stack draws for a search reads Common.Search until its caller passes a
/// text of its own: the field itself and the three hosts that draw one.</summary>
public sealed class SearchPlaceholderTests : BunitContext, IAsyncLifetime
{
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    void Arrange(string language)
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new TwoLanguageStrings("Common.Search", "Search", "Buscar")]));
        Services.AddSingleton<LanguageProvider>(new LanguageProvider { Current = language });
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(SearchPlaceholderTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorization().SetAuthorized("search-placeholder");
    }

    static string? Hint(IRenderedComponent<NsSearchField> cut)
    {
        return cut.Find("input").GetAttribute("placeholder");
    }

    static string? Hint(IRenderedComponent<NsPageHeader> cut)
    {
        return cut.Find(".ns-toolbar-search input").GetAttribute("placeholder");
    }

    static string? Hint(IRenderedComponent<NsNavMenu> cut)
    {
        return cut.Find(".mud-input-slot").GetAttribute("placeholder");
    }

    static string? Hint(IRenderedComponent<NsTree<TreeNode>> cut)
    {
        return cut.Find("input").GetAttribute("placeholder");
    }

    static IRenderedComponent<NsTree<TreeNode>> RenderTree(BunitContext ctx, string? placeholder)
    {
        return ctx.Render<NsTree<TreeNode>>(p => p
            .Add(t => t.Items, [new TreeNode { Name = "Ventas" }])
            .Add(t => t.Children, node => node.Children)
            .Add(t => t.ItemText, node => node.Name)
            .Add(t => t.Searchable, true)
            .Add(t => t.SearchPlaceholder, placeholder));
    }

    [Theory]
    [InlineData("es", "Buscar")]
    [InlineData("en", "Search")]
    public void NsSearchField_ReadsCommonSearchByDefault(string language, string expected)
    {
        Arrange(language);

        Assert.Equal(expected, Hint(Render<NsSearchField>()));
    }

    [Fact]
    public void NsSearchField_ReadsThePlaceholderItWasGiven()
    {
        Arrange("es");

        Assert.Equal("Pedido", Hint(Render<NsSearchField>(p => p.Add(f => f.Placeholder, "Pedido"))));
    }

    [Theory]
    [InlineData("es", "Buscar")]
    [InlineData("en", "Search")]
    public void NsPageHeader_ReadsCommonSearchByDefault(string language, string expected)
    {
        Arrange(language);

        var cut = Render<NsPageHeader>(p => p
            .Add(h => h.Title, "Catálogo")
            .Add(h => h.SearchText, string.Empty)
            .Add(h => h.SearchTextChanged, _ => { }));

        Assert.Equal(expected, Hint(cut));
    }

    [Fact]
    public void NsPageHeader_ReadsTheSearchPlaceholderItWasGiven()
    {
        Arrange("es");

        var cut = Render<NsPageHeader>(p => p
            .Add(h => h.Title, "Catálogo")
            .Add(h => h.SearchText, string.Empty)
            .Add(h => h.SearchTextChanged, _ => { })
            .Add(h => h.SearchPlaceholder, "Pedido"));

        Assert.Equal("Pedido", Hint(cut));
    }

    [Theory]
    [InlineData("es", "Buscar")]
    [InlineData("en", "Search")]
    public void NsNavMenu_ReadsCommonSearchByDefault(string language, string expected)
    {
        Arrange(language);

        Assert.Equal(expected, Hint(Render<NsNavMenu>()));
    }

    [Fact]
    public void NsNavMenu_ReadsTheSearchPlaceholderItWasGiven()
    {
        Arrange("es");

        Assert.Equal("Pedido", Hint(Render<NsNavMenu>(p => p.Add(m => m.SearchPlaceholder, "Pedido"))));
    }

    [Theory]
    [InlineData("es", "Buscar")]
    [InlineData("en", "Search")]
    public void NsTree_ReadsCommonSearchByDefault(string language, string expected)
    {
        Arrange(language);

        Assert.Equal(expected, Hint(RenderTree(this, null)));
    }

    [Fact]
    public void NsTree_ReadsTheSearchPlaceholderItWasGiven()
    {
        Arrange("es");

        Assert.Equal("Pedido", Hint(RenderTree(this, "Pedido")));
    }
}
