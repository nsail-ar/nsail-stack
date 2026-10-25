// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The composer is one control because the rules of sending words are one set: Enter
/// sends, a blank box never sends, a send in flight never sends twice, and the face says Enviar
/// with the paper plane wherever it stands (ui/forms.md, Enter). What is held here is everything
/// the keystroke READS — the mark the Enter policy finds from the textarea it landed in, and the
/// state of the face it presses — because the keystroke itself cannot be proved at this layer:
/// AngleSharp performs no implicit form submission and runs no listener of ns.js
/// (NsEnterPolicyMarkTests' own split). The two gestures are walked in a browser.</summary>
public sealed class NsSendMessageTests : BunitContext, IAsyncLifetime
{
    public NsSendMessageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
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

    /// <summary>The mark the policy reaches for: the box the words are typed into stands inside
    /// the composer, which is how ns.js tells the one textarea whose Enter is not a line from
    /// every other textarea in the house — read from the keystroke's own target, upwards.</summary>
    [Fact]
    public void TheBoxStandsInsideTheComposerTheEnterPolicyLooksFor()
    {
        var cut = Render<SendMessageHost>();

        var textarea = cut.Find("textarea");

        Assert.Equal("true", textarea.GetAttribute("data-ns-field"));
        Assert.NotNull(textarea.Closest(".ns-send"));
    }

    /// <summary>What the policy presses, found from the composer the way ns.js finds it: the
    /// send is a real submit of the screen's own form, so the key runs the code the click runs
    /// and there is no second path a message leaves by.</summary>
    [Fact]
    public void TheFaceTheKeyPressesIsTheFormsOwnSubmit()
    {
        var cut = Render<SendMessageHost>();

        var send = cut.Find(".ns-send button[type=\"submit\"]");

        Assert.Equal("Common.Send", send.TextContent.Trim());
        Assert.NotNull(send.Closest("form"));
    }

    /// <summary>A blank box sends nothing: the face is greyed out, which is also what refuses the
    /// keystroke — ns.js presses no face a hand could not press, so the blank rule is written
    /// once.</summary>
    [Fact]
    public void ABlankBoxGreysTheFaceOut()
    {
        var cut = Render<SendMessageHost>();

        Assert.True(cut.Find(".ns-send button[type=\"submit\"]").HasAttribute("disabled"));
    }

    /// <summary>Whitespace is not a message. The box commits every keystroke (ui/fields.md), so
    /// the face follows what is held at this instant and a box holding spaces is still
    /// blank.</summary>
    [Fact]
    public async Task SpacesAloneLeaveTheFaceGreyedOut()
    {
        var cut = Render<SendMessageHost>();

        await cut.InvokeAsync(() => cut.Find("textarea").Input("   "));

        Assert.True(cut.Find(".ns-send button[type=\"submit\"]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task WordsTypedEnableTheFace()
    {
        var cut = Render<SendMessageHost>();

        await cut.InvokeAsync(() => cut.Find("textarea").Input("hola"));

        Assert.False(cut.Find(".ns-send button[type=\"submit\"]").HasAttribute("disabled"));
    }

    /// <summary>The words reach the screen's own model, which is what the submit sends: the box
    /// binds the member the message carries and nothing in between holds a copy of it.</summary>
    [Fact]
    public async Task ThePressSendsWhatTheBoxHolds()
    {
        var cut = Render<SendMessageHost>();

        await cut.InvokeAsync(() => cut.Find("textarea").Input("hola"));
        await cut.Find(".ns-send button[type=\"submit\"]").ClickAsync(new());

        Assert.Equal(["hola"], cut.Instance.Sent);
    }

    /// <summary>A send in flight greys the face out, so the second Enter of a fast hand presses
    /// nothing and one message does not leave twice (NsSubmit's FormRunning).</summary>
    [Fact]
    public async Task WhileTheSendIsInFlightTheFaceIsGreyedOut()
    {
        var holding = new TaskCompletionSource();
        var cut = Render<SendMessageHost>(p => p.Add(x => x.Holding, holding));

        await cut.InvokeAsync(() => cut.Find("textarea").Input("hola"));
        var press = cut.Find(".ns-send button[type=\"submit\"]").ClickAsync(new());

        Assert.True(cut.Find(".ns-send button[type=\"submit\"]").HasAttribute("disabled"));

        holding.SetResult();
        await press;
    }

    /// <summary>The act's place is one across every screen that sends: whatever a screen puts
    /// beside the face — a road to pick, a link out to the channel — stands before it in the same
    /// row, and the face is last.</summary>
    [Fact]
    public void WhatTheScreenPutsBesideTheFaceStandsBeforeIt()
    {
        var cut = Render<SendMessageHost>();

        var row = cut.Find(".ns-send button.beside").Closest("div")!;
        var controls = row.QuerySelectorAll("button");

        Assert.Equal(2, controls.Length);
        Assert.Contains("beside", controls[0].ClassList);
        Assert.Equal("submit", controls[1].GetAttribute("type"));
    }

    /// <summary>The box is empty again after a send, and that is the SCREEN's gesture: a fresh
    /// model, keyed, is a first render — which is what gives FocusOnRender the caret back. The
    /// composer holds no copy of the words, so nothing of it survives the new model.</summary>
    [Fact]
    public async Task AFreshModelEmptiesTheBox()
    {
        var cut = Render<SendMessageHost>();

        await cut.InvokeAsync(() => cut.Find("textarea").Input("hola"));
        await cut.Find(".ns-send button[type=\"submit\"]").ClickAsync(new());

        Assert.Equal(["hola"], cut.Instance.Sent);
        Assert.Empty(cut.Find("textarea").TextContent);
        Assert.True(cut.Find(".ns-send button[type=\"submit\"]").HasAttribute("disabled"));
    }

    /// <summary>The box DRAWS the words it was handed: the control holds no copy of them, so
    /// what a screen's model carries into it — a draft, a handover — is what the person reads in
    /// the box and what the send would carry back out.</summary>
    [Fact]
    public void TheBoxDrawsTheWordsItWasHanded()
    {
        var cut = Render<NsSendMessage>(ps => ps.Add(p => p.Text, "hola"));

        Assert.Equal("hola", cut.Find("textarea").TextContent);
        Assert.False(cut.Find("button[type=\"submit\"]").HasAttribute("disabled"));
    }

    /// <summary>A composer a screen switched off does not send BY ANY GESTURE: the greyed box
    /// beside a live face would let a click take the draft out past the very screen that said
    /// no — the keyboard never gets there (a disabled box takes no keystroke), which is exactly
    /// why the face has to answer for the state too.</summary>
    [Fact]
    public async Task ADisabledComposerGreysTheFaceAndNotOnlyTheBox()
    {
        var cut = Render<SendMessageHost>(p => p.Add(x => x.Disabled, true));

        await cut.InvokeAsync(() => cut.Find("textarea").Input("hola"));

        Assert.True(cut.Find("textarea").HasAttribute("disabled"));
        Assert.True(cut.Find(".ns-send button[type=\"submit\"]").HasAttribute("disabled"));
    }
}
