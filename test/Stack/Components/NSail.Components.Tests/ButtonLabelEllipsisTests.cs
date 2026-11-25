// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A button's label that does not fit holds one line and ends in «…», as a chip's
/// does. bUnit lays nothing out, so the cut is read off the shipped stylesheet and the
/// whisper's gate off ns.js; a render proves every face carries the span both select.</summary>
public sealed class ButtonLabelEllipsisTests : BunitContext, IAsyncLifetime
{
    const string Long = "0002 - MOSTRADOR (HOMOLOGACIÓN ARCA)";

    public ButtonLabelEllipsisTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new RouteTable(typeof(ButtonLabelEllipsisTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
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

    /// <summary>AC1: the clip is the label's own span, unscoped, and the vendor's content box
    /// only gives up its min-content floor while it keeps the inline-flex that centres the
    /// label; the root is held to its container.</summary>
    [Fact]
    public void EveryButtonsLabelHoldsOneLineAndEndsInAnEllipsis()
    {
        var css = Uncommented(HouseStylesheet.Read());

        var label = Body(css, @"\.ns-button-label");
        Assert.Matches(@"min-width:\s*0;", label);
        Assert.Matches(@"max-width:\s*100%;", label);
        Assert.Matches(@"overflow:\s*hidden;", label);
        Assert.Matches(@"text-overflow:\s*ellipsis;", label);
        Assert.Matches(@"white-space:\s*nowrap;", label);

        var content = Body(css, @"\.mud-button-label:has\(> \.ns-button-label\)");
        Assert.Matches(@"min-width:\s*0;", content);
        Assert.DoesNotMatch(@"display|align-items|justify-content", content);

        Assert.Matches(@"max-width:\s*100%;", Body(css, @"\.mud-button-root:has\(> \.mud-button-label > \.ns-button-label\)"));
    }

    /// <summary>K1: the rule sets nothing that changes a label that fits — no size, case,
    /// padding, display or place.</summary>
    [Fact]
    public void TheCutChangesNothingOnALabelThatFits()
    {
        var css = Uncommented(HouseStylesheet.Read());

        foreach (var body in new[]
        {
            Body(css, @"\.ns-button-label"),
            Body(css, @"\.mud-button-label:has\(> \.ns-button-label\)"),
            Body(css, @"\.mud-button-root:has\(> \.mud-button-label > \.ns-button-label\)"),
        })
        {
            Assert.DoesNotMatch(@"font|text-transform|letter-spacing|padding|margin|display|(?<!-)width:|height|line-height|vertical-align|position", body);
        }
    }

    /// <summary>K3: the faces that style their own label keep their rules, scoped under their
    /// container.</summary>
    [Fact]
    public void TheFacesThatStyleTheirOwnLabelKeepTheirLook()
    {
        var css = Uncommented(HouseStylesheet.Read());

        var channel = Body(css, @"\.ns-contact-channel \.ns-button-label");
        Assert.Matches(@"text-transform:\s*none;", channel);
        Assert.Matches(@"display:\s*inline-block;", channel);
        Assert.Matches(@"vertical-align:\s*bottom;", channel);

        var tile = Body(css, @"\.ns-sign-in-providers \.ns-button-label");
        Assert.Matches(@"font-size:\s*0\.6875rem;", tile);
        Assert.Matches(@"line-height:\s*1\.1;", tile);
        Assert.Matches(@"text-transform:\s*none;", tile);

        Assert.Matches(@"text-transform:\s*none;", Body(css, @"\.ns-disclosure \.ns-button-label"));
    }

    /// <summary>A pressable column heading is a button that reads as a plain heading: its word
    /// wraps as the plain one beside it does, so a long heading buys no width (NsTh's Whole).
    /// </summary>
    [Fact]
    public void APressableHeadingStillWraps()
    {
        var css = Uncommented(HouseStylesheet.Read());

        var word = Body(css, @"\.ns-table \.mud-table-head \.ns-th-order \.ns-button-label");
        Assert.Matches(@"white-space:\s*normal;", word);
        Assert.Matches(@"overflow:\s*visible;", word);
        Assert.Matches(@"min-width:\s*auto;", word);
        Assert.Matches(@"max-width:\s*none;", word);

        Assert.Matches(@"min-width:\s*auto;", Body(css, @"\.ns-table \.mud-table-head \.ns-th-order \.mud-button-label"));
        Assert.Matches(@"max-width:\s*none;", Body(css, @"\.ns-table \.mud-table-head \.ns-th-order\.mud-button-root"));
    }

    /// <summary>AC1: every face that draws a word carries the span the cut selects.</summary>
    [Fact]
    public void EveryWordedFaceCarriesTheLabelSpan()
    {
        var button = Render<NsButton>(p => p
            .Add(x => x.Label, Long)
            .Add(x => x.Icon, NsIcons.Till)
            .Add(x => x.As, NsAs.Inline));
        var link = Render<NsLink>(p => p
            .Add(x => x.Href, "/points-of-sale")
            .Add(x => x.As, NsAs.Default)
            .Add(x => x.Label, Long)
            .Add(x => x.Icon, NsIcons.Till));
        var submit = Render<NsSubmit>();

        Assert.Equal(Long, button.Find(".mud-button-label > .ns-button-label").TextContent);
        Assert.Equal(Long, link.Find(".mud-button-label > .ns-button-label").TextContent);
        Assert.NotEmpty(submit.Find(".mud-button-label > .ns-button-label").TextContent.Trim());
    }

    /// <summary>AC2: a long label is still whole in the accessible name and the whisper's
    /// text.</summary>
    [Fact]
    public void ALongLabelIsWholeInTheNameAndTheWhisper()
    {
        var button = Render<NsButton>(p => p
            .Add(x => x.Label, Long)
            .Add(x => x.Icon, NsIcons.Till)
            .Add(x => x.As, NsAs.Inline))
            .Find("button");
        var link = Render<NsLink>(p => p
            .Add(x => x.Href, "/points-of-sale")
            .Add(x => x.As, NsAs.Default)
            .Add(x => x.Label, Long)
            .Add(x => x.Icon, NsIcons.Till))
            .Find("a");

        Assert.Equal(Long, button.GetAttribute("aria-label"));
        Assert.Equal(Long, button.GetAttribute("data-ns-whisper"));
        Assert.Equal(Long, link.GetAttribute("aria-label"));
        Assert.Equal(Long, link.GetAttribute("data-ns-whisper"));
    }

    /// <summary>AC2: innerText reports a cut label whole, so the whisper's gate asks the span
    /// itself whether it was cut before it compares the words.</summary>
    [Fact]
    public void TheWhisperOpensOverACutLabel()
    {
        var script = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src/Stack/BaseServices/NSail.BaseServices.WebApp/wwwroot/ns.js"));

        var gate = Regex.Match(script, @"function whisperAdds\(el, text\) \{(?<body>.*?)\n    \}", RegexOptions.Singleline).Groups["body"].Value;

        Assert.Contains("el.querySelector(\":scope > .mud-button-label > .ns-button-label\")", gate);
        Assert.Contains("label.scrollWidth > label.clientWidth", gate);
        Assert.True(gate.IndexOf("scrollWidth", StringComparison.Ordinal) < gate.IndexOf("el.innerText", StringComparison.Ordinal));
    }

    static string Body(string css, string selector)
    {
        var rule = Regex.Match(css, @"(?:^|\})\s*" + selector + @"\s*\{(?<body>[^}]*)\}");

        Assert.True(rule.Success, $"ns-mud.css no longer carries a rule for '{selector}'.");

        return rule.Groups["body"].Value;
    }

    static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }

    static string Uncommented(string css)
    {
        return Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
    }
}
