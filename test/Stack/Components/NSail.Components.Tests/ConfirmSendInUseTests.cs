// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Components.Tests;

/// <summary>Every CRUD grid carries a delete, and a delete the server refuses because
/// something still points at the row has no field to draw under. The contract pinned here:
/// the handler names a CODE and nothing else, the client owns every word, and the words come
/// from the message type's own key with a Stack-wide fallback — the same derivation
/// NotifySent and the confirmation itself already use. It arrives as a dialog, never a toast:
/// a refusal that fades is the silent failure principles.md forbids.</summary>
public sealed class ConfirmSendInUseTests : BunitContext
{
    const string Fallback = "Este registro está en uso y no se puede borrar. Para dejar de verlo, deshabilitalo.";

    readonly CountingDialogManager _dialogs = new();
    readonly RefusingMediator _mediator = new();

    // Written into before the send, never after: StringCatalog merges its sources once per
    // language and caches, and the first read happens inside the send under test.
    readonly Dictionary<string, string> _strings = new()
    {
        ["Common.ConfirmSend"] = "¿Confirmás esta acción?",
        ["Common.InUse"] = Fallback,
    };

    public ConfirmSendInUseTests()
    {
        Services.AddSingleton(new StringCatalog([new FixedStrings(_strings)]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(ConfirmSendInUseTests).Assembly, []));
        Services.AddSingleton<DialogManager>(_dialogs);
        Services.AddSingleton<Mediator>(_mediator);
        Services.AddSingleton<MessageContextAccessor>();
    }

    async Task<bool> Delete()
    {
        var probe = Render<ConfirmSendProbe>();

        return await probe.Instance.Run(new DeleteProbeThing());
    }

    /// <summary>The specific key wins, and it is derived from the message type — the send is
    /// what names the text, so no screen writes a key anywhere.</summary>
    [Fact]
    public async Task ARefusalWithTheMessagesOwnKey_ShowsThatKeysText()
    {
        _strings["Components.DeleteProbeThing.InUseMessage"] = "Este Color está en uso y no se puede borrar.";
        _mediator.Refusal = BusinessProblem.InUse("Color");

        var sent = await Delete();

        Assert.False(sent);
        Assert.Equal(["Este Color está en uso y no se puede borrar."], _dialogs.Alerts);
    }

    /// <summary>No key for this message: the Stack's own sentence answers, so a kit that adds a
    /// delete before it adds a string still refuses in words.</summary>
    [Fact]
    public async Task ARefusalWithNoKeyOfItsOwn_FallsBackToTheStacksSentence()
    {
        _mediator.Refusal = BusinessProblem.InUse("Color");

        var sent = await Delete();

        Assert.False(sent);
        Assert.Equal([Fallback], _dialogs.Alerts);
    }

    /// <summary>The placement half of the contract, and the reason this is not a Notify: the
    /// refusal waits for the person instead of racing their eye.</summary>
    [Fact]
    public async Task TheRefusalIsADialogAndNotAToast()
    {
        _mediator.Refusal = BusinessProblem.InUse("Color");

        await Delete();

        Assert.Single(_dialogs.Alerts);
        Assert.Empty(_dialogs.Notices);
    }

    /// <summary>Only InUse is absorbed here. Anything else keeps escaping to the Runner, which
    /// is what routes a Problem to the surface and its ProblemManager — a catch that widened
    /// would swallow every other refusal on the way.</summary>
    [Fact]
    public async Task ARefusalOfAnyOtherCode_StillEscapesToTheRunner()
    {
        _mediator.Refusal = BusinessProblem.RuleViolation("SystemRole", "System roles cannot be deleted.");

        var error = await Assert.ThrowsAsync<BusinessException>(Delete);

        Assert.Equal("RuleViolation", error.Code);
        Assert.Empty(_dialogs.Alerts);
    }

    /// <summary>The confirmation still gates the send: a refused prompt sends nothing, and so
    /// reaches no refusal to translate.</summary>
    [Fact]
    public async Task ADeclinedConfirmation_SendsNothing()
    {
        _dialogs.Answer = false;
        _mediator.Refusal = BusinessProblem.InUse("Color");

        var sent = await Delete();

        Assert.False(sent);
        Assert.Equal(0, _mediator.Sends);
        Assert.Empty(_dialogs.Alerts);
    }

    /// <summary>And the accepted, unrefused case is untouched: it sends and answers true.</summary>
    [Fact]
    public async Task AnAcceptedSendWithNoRefusal_ReportsItWasSent()
    {
        var sent = await Delete();

        Assert.True(sent);
        Assert.Equal(1, _mediator.Sends);
        Assert.Empty(_dialogs.Alerts);
    }
}
