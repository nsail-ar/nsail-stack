// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A caller that never asks keeps the field's shape from before this parameter
/// existed — fixed at four lines (nsail#1020, the Plantillas message body that used to hide a
/// six-line notice behind a scrollbar). MaxLines is the sentinel: unset, the vendor's fixed
/// sizing and Lines' own default draw exactly as they always did; set, the field grows with its
/// content between Lines and MaxLines instead.</summary>
public sealed class NsTextAreaGrowthTests : BunitContext
{
    public NsTextAreaGrowthTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void An_untouched_field_stays_fixed_at_four_lines()
    {
        var cut = Render<NsTextArea>(ps => ps.Add(p => p.Value, "hi"));

        var textarea = cut.Find("textarea");
        var control = cut.Find(".mud-input-control");

        Assert.Equal("4", textarea.GetAttribute("rows"));
        Assert.Contains("mud-input-sizing-fixed", control.ClassList);
        Assert.DoesNotContain("mud-input-sizing-auto", control.ClassList);
    }

    [Fact]
    public void Lines_alone_raises_the_fixed_height_without_growing()
    {
        var cut = Render<NsTextArea>(ps => ps
            .Add(p => p.Value, "hi")
            .Add(p => p.Lines, 8));

        var textarea = cut.Find("textarea");
        var control = cut.Find(".mud-input-control");

        Assert.Equal("8", textarea.GetAttribute("rows"));
        Assert.Contains("mud-input-sizing-fixed", control.ClassList);
    }

    [Fact]
    public void MaxLines_turns_on_the_vendors_own_auto_grow_between_the_two_bounds()
    {
        var cut = Render<NsTextArea>(ps => ps
            .Add(p => p.Value, "hi")
            .Add(p => p.Lines, 8)
            .Add(p => p.MaxLines, 20));

        var textarea = cut.Find("textarea");
        var control = cut.Find(".mud-input-control");

        Assert.Equal("8", textarea.GetAttribute("rows"));
        Assert.Contains("mud-input-sizing-auto", control.ClassList);
    }
}
