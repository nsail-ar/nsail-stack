// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

public sealed class NsSubmitIntentionTests : BunitContext, IAsyncLifetime
{
    public NsSubmitIntentionTests()
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

    // Send is the hero's own chrome with the paper airplane: a submit that sends words wears the
    // act it performs, and the fill stays the accent the tick's hero wears.
    [Fact]
    public void Send_keeps_the_heros_fill_and_wears_the_paper_airplane()
    {
        var send = Render<NsSubmit>(p => p.Add(x => x.As, NsAs.Send));
        var submit = Render<NsSubmit>();

        Assert.Contains(GlyphPath.Of(NsIcons.Send), Paths(send));
        Assert.DoesNotContain(GlyphPath.Of(NsIcons.Check), Paths(send));
        Assert.Contains(GlyphPath.Of(NsIcons.Check), Paths(submit));

        Assert.Contains("mud-button-filled-primary", send.Find("button").ClassName, StringComparison.Ordinal);
    }

    static IEnumerable<string?> Paths(IRenderedComponent<NsSubmit> button)
    {
        return button.FindAll("path").Select(path => path.GetAttribute("d"));
    }
}
