// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The form is `novalidate`, which withdraws more than the browser's bubble over an
/// empty marked box: an `input type="url"` is refused natively for its FORMAT whatever its
/// `required` says, and the one real URL box in the house binds a member that can declare no
/// rule of its own (a Channels template setting, one string for every kind). So the gate is the
/// field's — the date box's `GetOwnProblem` seam — and this is the proof that it stands in the
/// house's own words, blocks the submit, says nothing before Save, and takes what the browser
/// took — everywhere but a domain no parser can read, which is pinned here as the one place the
/// rule is stricter than the gate (fields.md carries the cost that buys).</summary>
public sealed class NsUrlRefusalTests : BunitContext, IAsyncLifetime
{
    const string Refusal = "Esto no es una dirección web — escribila completa, como https://ejemplo.com";
    const string DomainRefusal = "Esto no es una dirección web — revisá el dominio";

    public NsUrlRefusalTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog(
        [
            new TwoLanguageStrings(
                "Problems.UnreadableAddress",
                "This is not a web address — write it whole, like https://example.com",
                Refusal),
            new TwoLanguageStrings(
                "Problems.UnreadableAddressDomain",
                "This is not a web address — check the domain",
                DomainRefusal)
        ]));
        Services.AddSingleton(new LanguageProvider { Current = "es" });
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
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

    [Fact]
    public async Task AnAddressWithNoScheme_BlocksTheSubmitAndSaysSoUnderItsBox()
    {
        var model = new RefusedAddressModel();
        var submitted = false;

        var cut = Render<RefusedAddressHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await Type(cut, "www.optica.example/resenas");
        await Save(cut);

        Assert.False(submitted);

        var field = cut.FindComponent<NsUrlField>();

        Assert.NotEmpty(field.FindAll(".mud-input-error"));

        // The session's language, naming no member and quoting no pattern — which is the whole
        // reason this is not the browser's own sentence.
        Assert.Equal(Refusal, field.Find(".mud-input-helper-text").TextContent.Trim());
    }

    /// <summary>Without this the test above passes for the wrong reason: a gate that refuses
    /// every address refuses the broken one too. Each of the three last values is one Chromium
    /// takes — its URL parser escapes the space and normalizes the brace and the backslash — and
    /// a box that refused them would open an install's saved Link refused and hold every Guardar
    /// on that screen.</summary>
    [Theory]
    [InlineData("https://optica.example/resenas")]
    [InlineData("https://optica.example/mi local")]
    [InlineData("https://optica.example/{token}")]
    [InlineData("https://optica.example/a\\b")]
    public async Task AnAddressTheBrowserWouldHaveTaken_Submits(string address)
    {
        var model = new RefusedAddressModel();
        var submitted = false;

        var cut = Render<RefusedAddressHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await Type(cut, address);
        await Save(cut);

        Assert.True(submitted);
        Assert.Empty(cut.FindComponent<NsUrlField>().FindAll(".mud-input-error"));
    }

    /// <summary>An address that never got as far as announcing a domain — no scheme at all, the
    /// protocol-relative shape (which `Uri` alone reads as a path and hands back a `file` scheme
    /// the text never carried), a scheme with nothing after it, a single slash, a drive path. The
    /// announcement is what is missing, so the sentence is the one about writing it whole.
    /// Measured against Chromium: it refuses the first three and takes the last two — which is
    /// the stricter class the summary above names, and `escribila completa, como
    /// https://ejemplo.com` is the motive for those two as well.</summary>
    [Theory]
    [InlineData("optica.example")]
    [InlineData("//optica.example/resenas")]
    [InlineData("https:")]
    [InlineData("https:/optica.example")]
    [InlineData("C:\\resenas\\optica")]
    public async Task AnAddressThatNamesNoDomain_IsRefusedAndSaysToWriteItWhole(string address)
    {
        var cut = await Refused(address);

        Assert.Equal(Refusal, cut.FindComponent<NsUrlField>().Find(".mud-input-helper-text").TextContent.Trim());
    }

    /// <summary>The text said `scheme://` and no `Uri` can read what follows, so the defect is
    /// where the domain stands and the sentence says so: `https://optica example/x` IS written
    /// whole, and telling that person to write it whole names a defect that is not there — the
    /// house rule this story enforces. Measured against Chromium: it TAKES the first two, which
    /// is the rest of the stricter class, and refuses the last two.</summary>
    [Theory]
    [InlineData("https://optica example/resenas")]
    [InlineData("http://exam ple.com")]
    [InlineData("https://optica.example:99999")]
    [InlineData("http://")]
    public async Task AnAddressWhoseDomainNoParserCanRead_IsRefusedAndSaysTheDomain(string address)
    {
        var cut = await Refused(address);

        Assert.Equal(DomainRefusal, cut.FindComponent<NsUrlField>().Find(".mud-input-helper-text").TextContent.Trim());
    }

    async Task<IRenderedComponent<RefusedAddressHost>> Refused(string address)
    {
        var model = new RefusedAddressModel();
        var submitted = false;

        var cut = Render<RefusedAddressHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await Type(cut, address);
        await Save(cut);

        Assert.False(submitted);
        Assert.NotEmpty(cut.FindComponent<NsUrlField>().FindAll(".mud-input-error"));

        return cut;
    }

    /// <summary>A format rule is not a required rule: the box this field is really bound to is
    /// an optional setting, and a setting left blank is the install saying it ships the
    /// declaration's own default.</summary>
    [Fact]
    public async Task AnEmptyBoxNobodyMarked_IsNotRefused()
    {
        var model = new RefusedAddressModel();
        var submitted = false;

        var cut = Render<RefusedAddressHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await Save(cut);

        Assert.True(submitted);
        Assert.Empty(cut.FindComponent<NsUrlField>().FindAll(".mud-input-error"));
    }

    /// <summary>The refusal belongs to Save, and this field reads it off `Value` on every render,
    /// which is the one way a field can start drawing it before anybody asked: a screen opening
    /// over an address an older install saved would greet the person refused
    /// (intentional-ui.md — Save, never the keystroke).</summary>
    [Fact]
    public void AFormOpeningOverAnAddressTheModelBrought_DrawsNothing()
    {
        var model = new RefusedAddressModel { Link = "www.optica.example/resenas" };

        var cut = Render<RefusedAddressHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => { }));

        Assert.Empty(cut.FindComponent<NsUrlField>().FindAll(".mud-input-error"));
        Assert.DoesNotContain(Refusal, cut.Markup);
    }

    /// <summary>The box is `Immediate`, so the value travels per keystroke — and an address is
    /// unreadable for every letter of its scheme before it is readable at all. A form nobody has
    /// submitted stays clean through all of them.</summary>
    [Fact]
    public async Task AnAddressBeingTypedIntoAFormNobodyHasSubmitted_IsNotRefusedYet()
    {
        var model = new RefusedAddressModel();

        var cut = Render<RefusedAddressHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => { }));

        foreach (var letter in new[] { "h", "ht", "http", "https:" })
        {
            await Type(cut, letter);

            Assert.Empty(cut.FindComponent<NsUrlField>().FindAll(".mud-input-error"));
            Assert.DoesNotContain(Refusal, cut.Markup);
        }
    }

    /// <summary>From the first Save on, the field follows the value without being asked again —
    /// the parameter set the binding's write triggers is the seam, and it is the whole of what the
    /// field owes once its own `GetErrorText` is not drawing the refusal straight.</summary>
    [Fact]
    public async Task AnAddressCorrectedAfterARefusedSave_LiftsTheRefusalAndSubmits()
    {
        var model = new RefusedAddressModel();
        var submitted = false;

        var cut = Render<RefusedAddressHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await Type(cut, "www.optica.example/resenas");
        await Save(cut);

        Assert.False(submitted);

        await Type(cut, "https://optica.example/resenas");

        Assert.Empty(cut.FindComponent<NsUrlField>().FindAll(".mud-input-error"));

        await Save(cut);

        Assert.True(submitted);
    }

    static Task Type(IRenderedComponent<RefusedAddressHost> cut, string text)
    {
        return cut.InvokeAsync(() => cut.FindComponent<NsUrlField>().Find("input").Input(text));
    }

    static Task Save(IRenderedComponent<RefusedAddressHost> cut)
    {
        return cut.InvokeAsync(() => cut.Find("form").Submit());
    }
}
